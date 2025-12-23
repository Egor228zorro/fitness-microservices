<?php

declare(strict_types=1);

namespace Rebuilder\Training;

use PDO;
use PDOStatement;
use Psr\Container\ContainerInterface;
use Psr\Http\Message\ResponseInterface as Response;
use Psr\Http\Message\ServerRequestInterface as Request;
use Rebuilder\Training\Common\EnsiErrorHandler;
use Rebuilder\Training\Database\DatabaseConnection;
use Rebuilder\Training\Service\WorkoutService;
use Slim\App;
use Slim\Factory\AppFactory;
use Throwable;

class Application
{
    /** @var App<ContainerInterface|null> */
    private App $app;
    private PDO $db;
    private WorkoutService $workoutService;

    public function __construct()
    {
        error_reporting(E_ALL);
        ini_set('display_errors', '1');

        $this->app = AppFactory::create();

        // НАСТРОЙКА ПАРСЕРА ДЛЯ JSON
        $this->app->addBodyParsingMiddleware();

        // Добавляем кастомный парсер для JSON если не работает стандартный
        $this->app->add(function (Request $request, $handler) {
            $contentType = $request->getHeaderLine('Content-Type');

            if (strstr($contentType, 'application/json')) {
                $rawBody = (string)$request->getBody();
                if (!empty($rawBody)) {
                    $parsed = json_decode($rawBody, true);
                    if (json_last_error() === JSON_ERROR_NONE && is_array($parsed)) {
                        $request = $request->withParsedBody($parsed);
                    }
                }
            }

            return $handler->handle($request);
        });

        $this->db = DatabaseConnection::getInstance()->getConnection();
        $this->workoutService = new WorkoutService();

        $errorMiddleware = $this->app->addErrorMiddleware(
            true,  // displayErrorDetails
            true,  // logErrors
            true   // logErrorDetails
        );

        $errorMiddleware->setDefaultErrorHandler(function (
            Request $request,
            Throwable $exception,
            bool $displayErrorDetails,
            bool $logErrors,
            bool $logErrorDetails
        ) {
            $error = EnsiErrorHandler::serverError(
                $exception->getMessage(),
                $request->getUri()->getPath(),
                [
                    'exception' => get_class($exception),
                    'file' => $exception->getFile(),
                    'line' => $exception->getLine(),
                    'trace' => $displayErrorDetails ? $exception->getTrace() : []
                ]
            );

            $response = \Slim\Factory\AppFactory::determineResponseFactory()->createResponse();
            $json = json_encode($error, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR);
            $response->getBody()->write($json);

            $statusCode = $error['status'] ?? 500;
            $status = is_int($statusCode) ? $statusCode : 500;

            return $response
                ->withStatus($status)
                ->withHeader('Content-Type', 'application/json');
        });

        $this->setupRoutes();
    }

    /**
     * Безопасное получение строки из массива
     * @param array<string, mixed> $data
     */
    private function getString(array $data, string $key, string $default = ''): string
    {
        return isset($data[$key]) && is_string($data[$key]) ? $data[$key] : $default;
    }

    /**
     * Безопасное получение целого числа из массива
     * @param array<string, mixed> $data
     */
    private function getInt(array $data, string $key, int $default = 0): int
    {
        if (!isset($data[$key])) {
            return $default;
        }
        return is_numeric($data[$key]) ? (int)$data[$key] : $default;
    }

    /**
     * Безопасное выполнение PDO запроса с возвратом результата
     * @return array<string, mixed>|false
     */
    private function safeFetch(PDOStatement $stmt): array|false
    {
        $result = $stmt->fetch(PDO::FETCH_ASSOC);
        // fetch() возвращает либо array<string, mixed>, либо false
        if (!is_array($result)) {
            return false;
        }

        /** @var array<string, mixed> $result */
        return $result;
    }

    /**
     * Безопасное выполнение PDO запроса с возвратом всех результатов
     * @return array<int, array<string, mixed>>
     */
    private function safeFetchAll(PDOStatement $stmt): array
    {
        /** @var array<int, array<string, mixed>>|false $result */
        $result = $stmt->fetchAll(PDO::FETCH_ASSOC);
        // fetchAll() с PDO::FETCH_ASSOC всегда возвращает array<int, array<string, mixed>> или false
        if ($result === false) {
            return [];
        }

        // Теперь анализатор знает, что $result - это array
        return $result;
    }

    /**
     * Безопасное создание JSON ответа
     * @param array<array-key, mixed> $data
     */
    private function jsonResponse(Response $response, array $data, int $status = 200): Response
    {
        $json = json_encode($data,JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR);
        $response->getBody()->write($json);
        return $response
            ->withStatus($status)
            ->withHeader('Content-Type', 'application/json');
    }

    private function setupRoutes(): void
    {
        $db = $this->db;
        $workoutService = $this->workoutService;

        // ========== КОРНЕВЫЕ МАРШРУТЫ ==========

        $this->app->get('/', function (Request $request, Response $response) {
            return $this->jsonResponse($response, [
                'message' => 'Training Service is running!',
                'version' => '1.0.0',
                'endpoints' => [
                    '/health' => 'Health check',
                    '/db-test' => 'Database test',
                    '/test' => 'Test endpoint',
                    '/private/exercises' => 'Private exercises (GET/POST/PATCH/DELETE)',
                    '/private/workouts' => 'Private workouts (GET/POST/PATCH/DELETE)',
                    '/private/workouts/{id}/exercises' => 'Exercises in private workout',
                    '/private/workouts/{id}/tts' => 'Generate TTS',
                    '/private/workouts/{id}/tts/status' => 'TTS status',
                    '/workouts' => 'Admin workouts (GET/POST/PATCH/DELETE)',
                    '/exercises' => 'Admin exercises (GET/POST/PATCH/DELETE)'
                ]
            ]);
        });

        $this->app->get('/test', function (Request $request, Response $response) {
            return $this->jsonResponse($response, [
                'test' => 'OK',
                'timestamp' => date('Y-m-d H:i:s')
            ]);
        });

        $this->app->get('/health', function (Request $request, Response $response) {
            return $this->jsonResponse($response, [
                'status' => 'ok',
                'service' => 'training-service',
                'database' => 'connected',
                'timestamp' => date('Y-m-d H:i:s')
            ]);
        });

        // Тест БД
        $this->app->get('/db-test', function (Request $request, Response $response) use ($db) {
            try {
                $stmt = $db->query("SELECT COUNT(*) as table_count FROM information_schema.tables WHERE table_schema = 'public'");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to execute query');
                }

                $result = $this->safeFetch($stmt);

                $tableCount = 0;
                if ($result !== false && array_key_exists('table_count', $result)) {
                    $value = $result['table_count'];
                    // Дополнительная проверка на скалярный тип
                    if (is_scalar($value) || $value === null) {
                        $tableCount = (int)$value;
                    }
                }

                return $this->jsonResponse($response, [
                    'status' => 'success',
                    'tables_count' => $tableCount,
                    'message' => 'Database connection successful'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/db-test');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // ========== ПРИВАТНЫЕ УПРАЖНЕНИЯ (/private/exercises) ==========

        $this->app->get('/private/exercises', function (Request $request, Response $response) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                $stmt = $db->prepare("SELECT * FROM exercises WHERE user_id = :user_id ORDER BY created_at DESC");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }

                $stmt->execute(['user_id' => $userId]);
                $exercises = $this->safeFetchAll($stmt);

                return $this->jsonResponse($response, [
                    'data' => $exercises,
                    'count' => count($exercises)
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/private/exercises');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->post('/private/exercises', function (Request $request, Response $response) use ($db) {
            try {
                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                if (empty($this->getString($data, 'name'))) {
                    $error = EnsiErrorHandler::validationError('Field "name" is required', '/private/exercises');
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                $stmt = $db->prepare("
                    INSERT INTO exercises (user_id, name, description, media_url) 
                    VALUES (:user_id, :name, :description, :media_url) 
                    RETURNING *
                ");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }

                $stmt->execute([
                    ':user_id' => $userId,
                    ':name' => $this->getString($data, 'name'),
                    ':description' => $this->getString($data, 'description'),
                    ':media_url' => $this->getString($data, 'media_url')
                ]);

                $exercise = $this->safeFetch($stmt);

                if ($exercise === false) {
                    throw new \Exception('Failed to create exercise');
                }

                return $this->jsonResponse($response->withStatus(201), [
                    'success' => true,
                    'data' => $exercise,
                    'message' => 'Exercise created'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/private/exercises');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->get('/private/exercises/{id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                $stmt = $db->prepare("SELECT * FROM exercises WHERE id = :id AND user_id = :user_id");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }

                $stmt->execute(['id' => $args['id'], 'user_id' => $userId]);
                $exercise = $this->safeFetch($stmt);

                if ($exercise === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found", "/private/exercises/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                return $this->jsonResponse($response, $exercise);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/private/exercises/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->patch('/private/exercises/{id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';
                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                // Проверяем существование упражнения
                $checkStmt = $db->prepare("SELECT id FROM exercises WHERE id = :id AND user_id = :user_id");
                if (!$checkStmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $checkStmt->execute(['id' => $args['id'], 'user_id' => $userId]);

                if ($this->safeFetch($checkStmt) === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found", "/private/exercises/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Формируем SQL для обновления
                $updates = [];
                $params = [':id' => $args['id']];

                if (isset($data['name']) && is_string($data['name'])) {
                    $updates[] = 'name = :name';
                    $params[':name'] = $data['name'];
                }

                if (isset($data['description']) && is_string($data['description'])) {
                    $updates[] = 'description = :description';
                    $params[':description'] = $data['description'];
                }

                if (isset($data['media_url']) && is_string($data['media_url'])) {
                    $updates[] = 'media_url = :media_url';
                    $params[':media_url'] = $data['media_url'];
                }

                if (empty($updates)) {
                    $error = EnsiErrorHandler::validationError('No fields to update', "/private/exercises/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                $updates[] = 'updated_at = CURRENT_TIMESTAMP';
                $sql = "UPDATE exercises SET " . implode(', ', $updates) . " WHERE id = :id AND user_id = :user_id RETURNING *";
                $params[':user_id'] = $userId;

                $stmt = $db->prepare($sql);
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute($params);

                $exercise = $this->safeFetch($stmt);

                if ($exercise === false) {
                    throw new \Exception('Failed to update exercise');
                }

                return $this->jsonResponse($response, [
                    'success' => true,
                    'data' => $exercise,
                    'message' => 'Exercise updated'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/private/exercises/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->delete('/private/exercises/{id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                // Проверяем существование упражнения
                $checkStmt = $db->prepare("SELECT id FROM exercises WHERE id = :id AND user_id = :user_id");
                if (!$checkStmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $checkStmt->execute(['id' => $args['id'], 'user_id' => $userId]);

                if ($this->safeFetch($checkStmt) === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found", "/private/exercises/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                $stmt = $db->prepare("DELETE FROM exercises WHERE id = :id AND user_id = :user_id");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute(['id' => $args['id'], 'user_id' => $userId]);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Exercise deleted successfully',
                    'exercise_id' => $args['id']
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/private/exercises/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // ========== ТРЕНИРОВКИ (/workouts) ==========

        $this->app->get('/workouts', function (Request $request, Response $response) use ($db) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        '/workouts',
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                $stmt = $db->query("SELECT * FROM workouts ORDER BY created_at DESC");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to execute query');
                }

                $workouts = $this->safeFetchAll($stmt);

                return $this->jsonResponse($response, [
                    'data' => $workouts
                ]);
            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/workouts');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->get('/workouts/{id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/workouts/{$args['id']}",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                // Получаем тренировку
                $stmt = $db->prepare("SELECT * FROM workouts WHERE id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);
                $workout = $this->safeFetch($stmt);

                if ($workout === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/workouts/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Получаем упражнения для этой тренировки
                $stmt = $db->prepare("
                    SELECT e.*, we.order_index, we.duration_seconds 
                    FROM exercises e
                    JOIN workout_exercises we ON e.id = we.exercise_id
                    WHERE we.workout_id = ?
                    ORDER BY we.order_index
                ");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);
                $exercises = $this->safeFetchAll($stmt);

                $workout['exercises'] = $exercises;

                return $this->jsonResponse($response, $workout);
            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/workouts/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->post('/workouts', function (Request $request, Response $response) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        '/workouts',
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                if (empty($this->getString($data, 'name'))) {
                    $error = EnsiErrorHandler::validationError('Field "name" is required', '/workouts');
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                $stmt = $db->prepare("
                    INSERT INTO workouts (user_id, name, type) 
                    VALUES (:user_id, :name, :type) 
                    RETURNING id
                ");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }

                $stmt->execute([
                    ':user_id' => $this->getString($data, 'user_id', $userId),
                    ':name' => $this->getString($data, 'name'),
                    ':type' => $this->getString($data, 'type', 'strength')
                ]);

                $result = $this->safeFetch($stmt);

                if ($result === false) {
                    throw new \Exception('Failed to create workout');
                }

                return $this->jsonResponse($response->withStatus(201), [
                    'success' => true,
                    'id' => $result['id'] ?? '',
                    'message' => 'Workout created'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/workouts');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->patch('/workouts/{id}', function (Request $request, Response $response, array $args) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/workouts/{$args['id']}",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                // Проверяем существование тренировки
                $stmt = $db->prepare("SELECT id FROM workouts WHERE id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);
                if ($this->safeFetch($stmt) === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/workouts/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Формируем SQL для обновления
                $updates = [];
                $params = [':id' => $args['id']];

                if (isset($data['name']) && is_string($data['name'])) {
                    $updates[] = 'name = :name';
                    $params[':name'] = $data['name'];
                }

                if (isset($data['type']) && is_string($data['type'])) {
                    $updates[] = 'type = :type';
                    $params[':type'] = $data['type'];
                }

                if (isset($data['user_id']) && is_string($data['user_id'])) {
                    $updates[] = 'user_id = :user_id';
                    $params[':user_id'] = $data['user_id'];
                }

                if (empty($updates)) {
                    $error = EnsiErrorHandler::validationError('No fields to update', "/workouts/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                $updates[] = 'updated_at = CURRENT_TIMESTAMP';
                $sql = "UPDATE workouts SET " . implode(', ', $updates) . " WHERE id = :id";
                $stmt = $db->prepare($sql);
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute($params);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Workout updated'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/workouts/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->delete('/workouts/{id}', function (Request $request, Response $response, array $args) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/workouts/{$args['id']}",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                // Проверяем существование тренировки
                $stmt = $db->prepare("SELECT id FROM workouts WHERE id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);
                if ($this->safeFetch($stmt) === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/workouts/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                $stmt = $db->prepare("DELETE FROM workouts WHERE id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Workout deleted'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/workouts/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // ========== ПРИВАТНЫЕ ТРЕНИРОВКИ (/private/workouts) ==========

        $this->app->get('/private/workouts', function (Request $request, Response $response) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                $stmt = $db->prepare("SELECT * FROM workouts WHERE user_id = :user_id ORDER BY created_at DESC");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute(['user_id' => $userId]);
                $workouts = $this->safeFetchAll($stmt);

                return $this->jsonResponse($response, [
                    'data' => $workouts,
                    'count' => count($workouts)
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/private/workouts');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->get('/private/workouts/{id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                $stmt = $db->prepare("SELECT * FROM workouts WHERE id = :id AND user_id = :user_id");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute(['id' => $args['id'], 'user_id' => $userId]);
                $workout = $this->safeFetch($stmt);

                if ($workout === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/private/workouts/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                return $this->jsonResponse($response, $workout);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/private/workouts/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->post('/private/workouts', function (Request $request, Response $response) use ($db) {
            try {
                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                if (empty($this->getString($data, 'name'))) {
                    $error = EnsiErrorHandler::validationError('Field "name" is required', '/private/workouts');
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                $stmt = $db->prepare("
                    INSERT INTO workouts (user_id, name, type) 
                    VALUES (:user_id, :name, :type) 
                    RETURNING *
                ");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }

                $stmt->execute([
                    ':user_id' => $userId,
                    ':name' => $this->getString($data, 'name'),
                    ':type' => $this->getString($data, 'type', 'strength')
                ]);

                $workout = $this->safeFetch($stmt);

                if ($workout === false) {
                    throw new \Exception('Failed to create workout');
                }

                return $this->jsonResponse($response->withStatus(201), [
                    'success' => true,
                    'data' => $workout,
                    'message' => 'Workout created'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/private/workouts');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->patch('/private/workouts/{id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';
                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                // Проверяем существование тренировки пользователя
                $checkStmt = $db->prepare("SELECT id FROM workouts WHERE id = :id AND user_id = :user_id");
                if (!$checkStmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $checkStmt->execute(['id' => $args['id'], 'user_id' => $userId]);

                if ($this->safeFetch($checkStmt) === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/private/workouts/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Формируем SQL для обновления
                $updates = [];
                $params = [':id' => $args['id']];

                if (isset($data['name']) && is_string($data['name'])) {
                    $updates[] = 'name = :name';
                    $params[':name'] = $data['name'];
                }

                if (isset($data['type']) && is_string($data['type'])) {
                    $updates[] = 'type = :type';
                    $params[':type'] = $data['type'];
                }

                if (empty($updates)) {
                    $error = EnsiErrorHandler::validationError('No fields to update', "/private/workouts/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                $updates[] = 'updated_at = CURRENT_TIMESTAMP';
                $sql = "UPDATE workouts SET " . implode(', ', $updates) . " WHERE id = :id AND user_id = :user_id RETURNING *";
                $params[':user_id'] = $userId;

                $stmt = $db->prepare($sql);
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute($params);

                $workout = $this->safeFetch($stmt);

                if ($workout === false) {
                    throw new \Exception('Failed to update workout');
                }

                return $this->jsonResponse($response, [
                    'success' => true,
                    'data' => $workout,
                    'message' => 'Workout updated'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/private/workouts/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->delete('/private/workouts/{id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                // Проверяем существование тренировки пользователя
                $checkStmt = $db->prepare("SELECT id FROM workouts WHERE id = :id AND user_id = :user_id");
                if (!$checkStmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $checkStmt->execute(['id' => $args['id'], 'user_id' => $userId]);

                if ($this->safeFetch($checkStmt) === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/private/workouts/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                $stmt = $db->prepare("DELETE FROM workouts WHERE id = :id AND user_id = :user_id");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute(['id' => $args['id'], 'user_id' => $userId]);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Workout deleted successfully',
                    'workout_id' => $args['id']
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/private/workouts/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // ========== УПРАВЛЕНИЕ УПРАЖНЕНИЯМИ В ПРИВАТНОЙ ТРЕНИРОВКЕ ==========

        // 1. GET /private/workouts/{id}/exercises - получить упражнения приватной тренировки
        $this->app->get('/private/workouts/{id}/exercises', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                // Проверяем, что тренировка принадлежит пользователю
                $checkStmt = $db->prepare("SELECT id FROM workouts WHERE id = :id AND user_id = :user_id");
                if (!$checkStmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $checkStmt->execute(['id' => $args['id'], 'user_id' => $userId]);

                if ($this->safeFetch($checkStmt) === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/private/workouts/{$args['id']}/exercises");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                $stmt = $db->prepare("
                    SELECT e.*, we.id as workout_exercise_id, we.order_index, we.duration_seconds, we.rest_seconds, we.created_at as added_at
                    FROM exercises e
                    JOIN workout_exercises we ON e.id = we.exercise_id
                    WHERE we.workout_id = :workout_id
                    ORDER BY we.order_index
                ");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute(['workout_id' => $args['id']]);
                $exercises = $this->safeFetchAll($stmt);

                return $this->jsonResponse($response, [
                    'data' => $exercises,
                    'count' => count($exercises)
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), "/private/workouts/{$args['id']}/exercises");
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // 2. POST /private/workouts/{id}/exercises - добавить упражнение в приватную тренировку
        $this->app->post('/private/workouts/{id}/exercises', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';
                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                // Проверяем, что тренировка принадлежит пользователю
                $checkStmt = $db->prepare("SELECT id FROM workouts WHERE id = :id AND user_id = :user_id");
                if (!$checkStmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $checkStmt->execute(['id' => $args['id'], 'user_id' => $userId]);

                if ($this->safeFetch($checkStmt) === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/private/workouts/{$args['id']}/exercises");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                if (empty($this->getString($data, 'exercise_id'))) {
                    $error = EnsiErrorHandler::validationError('Field "exercise_id" is required', "/private/workouts/{$args['id']}/exercises");
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                // Проверяем, что упражнение принадлежит пользователю
                $exerciseCheck = $db->prepare("SELECT id FROM exercises WHERE id = :exercise_id AND user_id = :user_id");
                if (!$exerciseCheck instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $exerciseCheck->execute(['exercise_id' => $this->getString($data, 'exercise_id'), 'user_id' => $userId]);

                if ($this->safeFetch($exerciseCheck) === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found or access denied", "/private/workouts/{$args['id']}/exercises");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                $stmt = $db->prepare("
                    INSERT INTO workout_exercises (workout_id, exercise_id, order_index, duration_seconds, rest_seconds)
                    VALUES (:workout_id, :exercise_id, :order_index, :duration_seconds, :rest_seconds)
                    RETURNING id
                ");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }

                $durationSeconds = null;
                if (isset($data['duration_seconds'])) {
                    /** @var mixed $durationValue */
                    $durationValue = $data['duration_seconds'];
                    if ($durationValue !== '' && is_numeric($durationValue)) {
                        $durationSeconds = (int)$durationValue;
                    }
                }

                $restSeconds = null;
                if (isset($data['rest_seconds'])) {
                    $value = filter_var($data['rest_seconds'], FILTER_VALIDATE_INT);
                    if ($value !== false) {
                        $restSeconds = $value;
                    }
                }

                $stmt->execute([
                    ':workout_id' => $args['id'],
                    ':exercise_id' => $this->getString($data, 'exercise_id'),
                    ':order_index' => $this->getInt($data, 'order_index'),
                    ':duration_seconds' => $durationSeconds,
                    ':rest_seconds' => $restSeconds
                ]);

                $result = $this->safeFetch($stmt);

                if ($result === false) {
                    throw new \Exception('Failed to add exercise to workout');
                }

                return $this->jsonResponse($response->withStatus(201), [
                    'success' => true,
                    'id' => $result['id'] ?? '',
                    'message' => 'Exercise added to workout'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), "/private/workouts/{$args['id']}/exercises");
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // 3. PATCH /private/workouts/{workout_id}/exercises/{we_id} - обновить упражнение в приватной тренировке
        $this->app->patch('/private/workouts/{workout_id}/exercises/{we_id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';
                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                // Проверяем, что тренировка принадлежит пользователю
                $workoutCheck = $db->prepare("SELECT id FROM workouts WHERE id = :id AND user_id = :user_id");
                if (!$workoutCheck instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $workoutCheck->execute(['id' => $args['workout_id'], 'user_id' => $userId]);

                if ($this->safeFetch($workoutCheck) === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/private/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Проверяем существование связи
                $checkStmt = $db->prepare("
                    SELECT we.id FROM workout_exercises we
                    JOIN workouts w ON we.workout_id = w.id
                    WHERE we.id = :we_id AND we.workout_id = :workout_id AND w.user_id = :user_id
                ");
                if (!$checkStmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $checkStmt->execute([
                    'we_id' => $args['we_id'],
                    'workout_id' => $args['workout_id'],
                    'user_id' => $userId
                ]);

                if ($this->safeFetch($checkStmt) === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found in this workout", "/private/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Формируем SQL для обновления
                $updates = [];
                $params = [':id' => $args['we_id']];

                if (isset($data['order_index']) && is_numeric($data['order_index'])) {
                    $updates[] = 'order_index = :order_index';
                    $params[':order_index'] = (int)$data['order_index'];
                }

                if (isset($data['duration_seconds'])) {
                    /** @var mixed $value */
                    $value = $data['duration_seconds'];
                    // Теперь анализатор не знает, что value не может быть null
                    if ($value === null || $value === '') {
                        $updates[] = 'duration_seconds = NULL';
                    } elseif (is_numeric($value)) {
                        $updates[] = 'duration_seconds = :duration_seconds';
                        $params[':duration_seconds'] = (int)$value;
                    }
                }


                if (array_key_exists('rest_seconds', $data)) {
                    $value = $data['rest_seconds'];
                    if ($value === '' || $value === null) {
                        $updates[] = 'rest_seconds = NULL';
                    } elseif (is_numeric($value)) {
                        $updates[] = 'rest_seconds = :rest_seconds';
                        $params[':rest_seconds'] = (int)$value;
                    }
                }

                if (empty($updates)) {
                    $error = EnsiErrorHandler::validationError('No fields to update', "/private/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                $sql = "UPDATE workout_exercises SET " . implode(', ', $updates) . " WHERE id = :id";
                $stmt = $db->prepare($sql);
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute($params);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Exercise updated in workout'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), "/private/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // 4. DELETE /private/workouts/{workout_id}/exercises/{we_id} - удалить упражнение из приватной тренировки
        $this->app->delete('/private/workouts/{workout_id}/exercises/{we_id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                // Проверяем, что тренировка принадлежит пользователю
                $workoutCheck = $db->prepare("SELECT id FROM workouts WHERE id = :id AND user_id = :user_id");
                if (!$workoutCheck instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $workoutCheck->execute(['id' => $args['workout_id'], 'user_id' => $userId]);

                if ($this->safeFetch($workoutCheck) === false) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/private/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Проверяем существование связи
                $checkStmt = $db->prepare("
                    SELECT we.id FROM workout_exercises we
                    JOIN workouts w ON we.workout_id = w.id
                    WHERE we.id = :we_id AND we.workout_id = :workout_id AND w.user_id = :user_id
                ");
                if (!$checkStmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $checkStmt->execute([
                    'we_id' => $args['we_id'],
                    'workout_id' => $args['workout_id'],
                    'user_id' => $userId
                ]);

                if ($this->safeFetch($checkStmt) === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found in this workout", "/private/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                $stmt = $db->prepare("DELETE FROM workout_exercises WHERE id = :id");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute(['id' => $args['we_id']]);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Exercise removed from workout'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), "/private/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // Генерация TTS для тренировки
        $this->app->get('/private/workouts/{id}/tts/status', function (Request $request, Response $response, array $args) use ($workoutService) {
            try {
                $workoutId = $args['id'];
                $status = $workoutService->getTtsStatus($workoutId);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'workout_id' => $workoutId,
                    'tts_status' => $status
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), "/private/workouts/{$args['id']}/tts/status");
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->post('/private/workouts/{id}/tts', function (Request $request, Response $response, array $args) use ($workoutService) {
            try {
                $workoutId = $args['id'];
                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                // Проверяем существование тренировки пользователя
                $workout = $workoutService->getWorkoutById($workoutId);
                if (!$workout) {
                    $error = EnsiErrorHandler::notFound("Workout not found", "/private/workouts/{$workoutId}/tts");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Генерируем TTS
                $jobId = $workoutService->generateTtsForWorkout($workoutId);

                return $this->jsonResponse($response->withStatus(202), [
                    'success' => true,
                    'job_id' => $jobId,
                    'workout_id' => $workoutId,
                    'workout_name' => $workout['name'] ?? 'Unknown',
                    'message' => 'TTS generation started',
                    'tts_service_endpoint' => '/status/' . $jobId,
                    'rabbitmq_queue' => 'tts_tasks'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/private/workouts/{id}/tts');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // ========== ПУБЛИЧНЫЕ УПРАЖНЕНИЯ (/exercises) ==========

        $this->app->get('/exercises', function (Request $request, Response $response) use ($db) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        '/exercises',
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                $stmt = $db->query("SELECT * FROM exercises ORDER BY created_at DESC");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to execute query');
                }
                $exercises = $this->safeFetchAll($stmt);

                return $this->jsonResponse($response, [
                    'data' => $exercises
                ]);
            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/exercises');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->get('/exercises/{id}', function (Request $request, Response $response, array $args) use ($db) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/exercises/{$args['id']}",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                $stmt = $db->prepare("SELECT * FROM exercises WHERE id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);
                $exercise = $this->safeFetch($stmt);

                if ($exercise === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found", "/exercises/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                return $this->jsonResponse($response, $exercise);
            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/exercises/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->post('/exercises', function (Request $request, Response $response) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        '/exercises',
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                if (empty($this->getString($data, 'name'))) {
                    $error = EnsiErrorHandler::validationError('Field "name" is required', '/exercises');
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                $userId = $request->getHeaderLine('X-User-Id') ?: '550e8400-e29b-41d4-a716-446655440000';

                $stmt = $db->prepare("
                    INSERT INTO exercises (user_id, name, description, media_url) 
                    VALUES (:user_id, :name, :description, :media_url) 
                    RETURNING id
                ");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }

                $stmt->execute([
                    ':user_id' => $this->getString($data, 'user_id', $userId),
                    ':name' => $this->getString($data, 'name'),
                    ':description' => $this->getString($data, 'description'),
                    ':media_url' => $this->getString($data, 'media_url')
                ]);

                $result = $this->safeFetch($stmt);

                if ($result === false) {
                    throw new \Exception('Failed to create exercise');
                }

                return $this->jsonResponse($response->withStatus(201), [
                    'success' => true,
                    'id' => $result['id'] ?? '',
                    'message' => 'Exercise created'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), '/exercises');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->patch('/exercises/{id}', function (Request $request, Response $response, array $args) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/exercises/{$args['id']}",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                // Проверяем существование упражнения
                $stmt = $db->prepare("SELECT id FROM exercises WHERE id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);
                if ($this->safeFetch($stmt) === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found", "/exercises/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Формируем SQL для обновления
                $updates = [];
                $params = [':id' => $args['id']];

                if (isset($data['name']) && is_string($data['name'])) {
                    $updates[] = 'name = :name';
                    $params[':name'] = $data['name'];
                }

                if (isset($data['description']) && is_string($data['description'])) {
                    $updates[] = 'description = :description';
                    $params[':description'] = $data['description'];
                }

                if (isset($data['media_url']) && is_string($data['media_url'])) {
                    $updates[] = 'media_url = :media_url';
                    $params[':media_url'] = $data['media_url'];
                }

                if (isset($data['user_id']) && is_string($data['user_id'])) {
                    $updates[] = 'user_id = :user_id';
                    $params[':user_id'] = $data['user_id'];
                }

                if (empty($updates)) {
                    $error = EnsiErrorHandler::validationError('No fields to update', "/exercises/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                $updates[] = 'updated_at = CURRENT_TIMESTAMP';
                $sql = "UPDATE exercises SET " . implode(', ', $updates) . " WHERE id = :id";
                $stmt = $db->prepare($sql);
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute($params);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Exercise updated'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/exercises/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->delete('/exercises/{id}', function (Request $request, Response $response, array $args) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/exercises/{$args['id']}",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                // Проверяем существование упражнения
                $stmt = $db->prepare("SELECT id FROM exercises WHERE id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);
                if ($this->safeFetch($stmt) === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found", "/exercises/{$args['id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                $stmt = $db->prepare("DELETE FROM exercises WHERE id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Exercise deleted'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), '/exercises/{id}');
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // ========== АДМИНСКИЕ УПРАВЛЕНИЕ УПРАЖНЕНИЯМИ В ТРЕНИРОВКЕ ==========

        $this->app->post('/workouts/{id}/exercises', function (Request $request, Response $response, array $args) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/workouts/{$args['id']}/exercises",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                if (empty($this->getString($data, 'exercise_id'))) {
                    $error = EnsiErrorHandler::validationError('Field "exercise_id" is required', "/workouts/{$args['id']}/exercises");
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                $durationSeconds = null;
                if (isset($data['duration_seconds']) && $data['duration_seconds'] !== '') {
                    $value = filter_var($data['duration_seconds'], FILTER_VALIDATE_INT);
                    if ($value !== false) {
                        $durationSeconds = $value;
                    }
                }

                $restSeconds = null;
                if (isset($data['rest_seconds']) && $data['rest_seconds'] !== '') {
                    $value = filter_var($data['rest_seconds'], FILTER_VALIDATE_INT);
                    if ($value !== false) {
                        $restSeconds = $value;
                    }
                }

                $stmt = $db->prepare("
                    INSERT INTO workout_exercises (workout_id, exercise_id, order_index, duration_seconds, rest_seconds)
                    VALUES (:workout_id, :exercise_id, :order_index, :duration_seconds, :rest_seconds)
                    RETURNING id
                ");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }

                $stmt->execute([
                    ':workout_id' => $args['id'],
                    ':exercise_id' => $this->getString($data, 'exercise_id'),
                    ':order_index' => $this->getInt($data, 'order_index'),
                    ':duration_seconds' => $durationSeconds,
                    ':rest_seconds' => $restSeconds
                ]);

                $result = $this->safeFetch($stmt);

                if ($result === false) {
                    throw new \Exception('Failed to add exercise to workout');
                }

                return $this->jsonResponse($response->withStatus(201), [
                    'success' => true,
                    'id' => $result['id'] ?? '',
                    'message' => 'Exercise added to workout'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), "/workouts/{$args['id']}/exercises");
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->patch('/workouts/{workout_id}/exercises/{we_id}', function (Request $request, Response $response, array $args) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/workouts/{$args['workout_id']}/exercises/{$args['we_id']}",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                $data = $request->getParsedBody() ?? [];
                if (!is_array($data)) {
                    $data = [];
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                // Проверяем существование связи
                $stmt = $db->prepare("SELECT id FROM workout_exercises WHERE id = ? AND workout_id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['we_id'], $args['workout_id']]);
                if ($this->safeFetch($stmt) === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found in this workout", "/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                // Формируем SQL для обновления
                $updates = [];
                $params = [':id' => $args['we_id']];

                if (isset($data['order_index']) && is_numeric($data['order_index'])) {
                    $updates[] = 'order_index = :order_index';
                    $params[':order_index'] = (int)$data['order_index'];
                }

                if (array_key_exists('duration_seconds', $data)) {
                    if ($data['duration_seconds'] === null || $data['duration_seconds'] === '') {
                        $updates[] = 'duration_seconds = NULL';
                    } elseif (is_numeric($data['duration_seconds'])) {
                        $updates[] = 'duration_seconds = :duration_seconds';
                        $params[':duration_seconds'] = (int)$data['duration_seconds'];
                    }
                }

                if (array_key_exists('rest_seconds', $data)) {
                    if ($data['rest_seconds'] === null || $data['rest_seconds'] === '') {
                        $updates[] = 'rest_seconds = NULL';
                    } elseif (is_numeric($data['rest_seconds'])) {
                        $updates[] = 'rest_seconds = :rest_seconds';
                        $params[':rest_seconds'] = (int)$data['rest_seconds'];
                    }
                }

                if (empty($updates)) {
                    $error = EnsiErrorHandler::validationError('No fields to update', "/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                    return $this->jsonResponse($response->withStatus(400), $error);
                }

                $sql = "UPDATE workout_exercises SET " . implode(', ', $updates) . " WHERE id = :id";
                $stmt = $db->prepare($sql);
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute($params);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Workout exercise updated'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), "/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->delete('/workouts/{workout_id}/exercises/{we_id}', function (Request $request, Response $response, array $args) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/workouts/{$args['workout_id']}/exercises/{$args['we_id']}",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                // Проверяем существование связи
                $stmt = $db->prepare("SELECT id FROM workout_exercises WHERE id = ? AND workout_id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['we_id'], $args['workout_id']]);
                if ($this->safeFetch($stmt) === false) {
                    $error = EnsiErrorHandler::notFound("Exercise not found in this workout", "/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                    return $this->jsonResponse($response->withStatus(404), $error);
                }

                $stmt = $db->prepare("DELETE FROM workout_exercises WHERE id = ?");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['we_id']]);

                return $this->jsonResponse($response, [
                    'success' => true,
                    'message' => 'Exercise removed from workout'
                ]);

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError($e->getMessage(), "/workouts/{$args['workout_id']}/exercises/{$args['we_id']}");
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        $this->app->get('/workouts/{id}/exercises', function (Request $request, Response $response, array $args) {
            try {
                $userRole = $request->getHeaderLine('X-User-Role') ?: 'user';

                if (!in_array($userRole, ['admin', 'moderator'])) {
                    $error = EnsiErrorHandler::unauthorized(
                        'Access denied. Admin role required',
                        "/workouts/{$args['id']}/exercises",
                        ['required_role' => 'admin or moderator']
                    );
                    return $this->jsonResponse($response->withStatus(403), $error);
                }

                require_once __DIR__ . '/../src/Database/DatabaseConnection.php';
                $db = DatabaseConnection::getInstance()->getConnection();

                $stmt = $db->prepare("
                    SELECT e.*, we.id as workout_exercise_id, we.order_index, we.duration_seconds, we.rest_seconds, we.created_at as added_at
                    FROM exercises e
                    JOIN workout_exercises we ON e.id = we.exercise_id
                    WHERE we.workout_id = ?
                    ORDER BY we.order_index
                ");
                if (!$stmt instanceof PDOStatement) {
                    throw new \Exception('Failed to prepare statement');
                }
                $stmt->execute([$args['id']]);
                $exercises = $this->safeFetchAll($stmt);

                return $this->jsonResponse($response, [
                    'data' => $exercises
                ]);
            } catch (\Exception $e) {
                $error = EnsiErrorHandler::databaseError($e->getMessage(), "/workouts/{$args['id']}/exercises");
                return $this->jsonResponse($response->withStatus(500), $error);
            }
        });

        // Тестовый маршрут - ПРОСТОЙ
        $this->app->post('/test-tts-route', function (Request $request, Response $response) {
            return $this->jsonResponse($response, [
                'test' => 'SUCCESS',
                'timestamp' => date('Y-m-d H:i:s'),
                'service' => 'training-service'
            ]);
        });

        // Favicon
        $this->app->get('/favicon.ico', function (Request $request, Response $response) {
            return $response->withStatus(204);
        });

        // ДОБАВЬТЕ ЭТОТ МАРШРУТ ДЛЯ ОТЛАДКИ
        $this->app->post('/debug-body', function (Request $request, Response $response) {
            $rawBody = (string)$request->getBody();
            $contentType = $request->getHeaderLine('Content-Type');
            $parsedBody = $request->getParsedBody();

            $data = [
                'parsed_body' => $parsedBody,
                'raw_body' => $rawBody,
                'content_type' => $contentType,
                'method' => $request->getMethod(),
                'php_input' => file_get_contents('php://input')
            ];

            $json = json_encode($data, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR);
            $response->getBody()->write($json);
            return $response->withHeader('Content-Type', 'application/json');
        });
    }

    public function run(): void
    {
        $this->app->run();
    }
}
