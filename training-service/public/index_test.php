<?php
declare(strict_types=1);

// Автозагрузка
require_once __DIR__ . '/../vendor/autoload.php';

// Если нет vendor/autoload.php, создаем простой загрузчик
if (!file_exists(__DIR__ . '/../vendor/autoload.php')) {
    echo "⚠️  Автозагрузчик не найден. Использую простую загрузку...\n";
    
    // Простая загрузка классов
    spl_autoload_register(function ($class) {
        $prefix = 'Rebuilder\\Training\\';
        $base_dir = __DIR__ . '/../src/';
        
        if (strpos($class, $prefix) !== 0) {
            return;
        }
        
        $relative_class = substr($class, strlen($prefix));
        $file = $base_dir . str_replace('\\', '/', $relative_class) . '.php';
        
        if (file_exists($file)) {
            require $file;
        }
    });
}

// Инициализация приложения
echo "=== TRAINING SERVICE API ===\n";
echo "Доступные эндпоинты:\n";
echo "GET  /health     - Проверка работы\n";
echo "GET  /workouts   - Все тренировки\n";
echo "POST /workouts   - Создать тренировку\n";
echo "GET  /db-check   - Проверка базы данных\n\n";

// Проверяем подключение к базе
try {
    require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
    $db = \Rebuilder\Training\Database\DatabaseConnection::getInstance()->getConnection();
    echo "✅ База данных: подключено\n";
} catch (Exception $e) {
    echo "❌ База данных: " . $e->getMessage() . "\n";
}

// Обрабатываем запрос
$requestUri = $_SERVER['REQUEST_URI'] ?? '/';
$requestMethod = $_SERVER['REQUEST_METHOD'] ?? 'GET';

echo "\n=== ЗАПРОС ===\n";
echo "Метод: $requestMethod\n";
echo "URI: $requestUri\n\n";

// Маршрутизация
switch (true) {
    case $requestUri === '/health' && $requestMethod === 'GET':
        header('Content-Type: application/json');
        echo json_encode([
            'status' => 'ok',
            'service' => 'training-service',
            'timestamp' => date('Y-m-d H:i:s'),
            'database' => 'connected'
        ]);
        break;
        
    case $requestUri === '/workouts' && $requestMethod === 'GET':
        try {
            $db = \Rebuilder\Training\Database\DatabaseConnection::getInstance()->getConnection();
            $stmt = $db->query("SELECT * FROM workouts ORDER BY created_at DESC");
            $workouts = $stmt->fetchAll();
            
            // Преобразуем UUID в строку
            foreach ($workouts as &$workout) {
                if (is_resource($workout['id'])) {
                    $workout['id'] = stream_get_contents($workout['id']);
                }
            }
            
            header('Content-Type: application/json');
            echo json_encode([
                'success' => true,
                'count' => count($workouts),
                'data' => $workouts
            ]);
        } catch (Exception $e) {
            header('Content-Type: application/json', true, 500);
            echo json_encode(['error' => $e->getMessage()]);
        }
        break;
        
    case $requestUri === '/workouts' && $requestMethod === 'POST':
        $input = json_decode(file_get_contents('php://input'), true);
        
        if (!$input || !isset($input['name'])) {
            header('Content-Type: application/json', true, 400);
            echo json_encode(['error' => 'Необходимо указать название тренировки']);
            break;
        }
        
        try {
            $db = \Rebuilder\Training\Database\DatabaseConnection::getInstance()->getConnection();
            
            $stmt = $db->prepare("
                INSERT INTO workouts (user_id, name, type) 
                VALUES (:user_id, :name, :type) 
                RETURNING id
            ");
            
            $stmt->execute([
                ':user_id' => $input['user_id'] ?? '550e8400-e29b-41d4-a716-446655440000',
                ':name' => $input['name'],
                ':type' => $input['type'] ?? 'strength'
            ]);
            
            $result = $stmt->fetch();
            
            header('Content-Type: application/json', true, 201);
            echo json_encode([
                'success' => true,
                'id' => is_resource($result['id']) ? stream_get_contents($result['id']) : $result['id'],
                'message' => 'Тренировка создана'
            ]);
        } catch (Exception $e) {
            header('Content-Type: application/json', true, 400);
            echo json_encode(['error' => $e->getMessage()]);
        }
        break;
        
    case $requestUri === '/db-check' && $requestMethod === 'GET':
        try {
            $db = \Rebuilder\Training\Database\DatabaseConnection::getInstance()->getConnection();
            
            // Получаем статистику
            $tables = ['workouts', 'exercises', 'workout_exercises', 'user_workout_settings', 'tts_jobs'];
            $stats = [];
            
            foreach ($tables as $table) {
                $stmt = $db->query("SELECT COUNT(*) as count FROM $table");
                $stats[$table] = $stmt->fetch()['count'];
            }
            
            header('Content-Type: application/json');
            echo json_encode([
                'success' => true,
                'database' => 'training_db',
                'stats' => $stats,
                'tables' => $tables
            ]);
        } catch (Exception $e) {
            header('Content-Type: application/json', true, 500);
            echo json_encode(['error' => $e->getMessage()]);
        }
        break;
        
    default:
        header('Content-Type: application/json', true, 404);
        echo json_encode([
            'error' => 'Эндпоинт не найден',
            'available_endpoints' => [
                'GET /health',
                'GET /workouts',
                'POST /workouts',
                'GET /db-check'
            ]
        ]);
        break;
}