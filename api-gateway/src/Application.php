<?php

namespace Rebuilder\ApiGateway;

use Firebase\JWT\JWT;
use Firebase\JWT\Key;
use GuzzleHttp\Client;
use Psr\Container\ContainerInterface;
use Psr\Http\Message\ResponseInterface as Response;
use Psr\Http\Message\ServerRequestInterface as Request;
use Rebuilder\ApiGateway\Common\EnsiErrorHandler;
use Slim\App;
use Slim\Factory\AppFactory;

class Application
{
    /** @var App<ContainerInterface|null> */
    private App $app;

    /** @var Client */
    private Client $httpClient;

    /** @var string */
    private string $jwtSecret;

    /** @var string */
    private string $jwtAlgorithm;

    public function __construct()
    {
        $this->app = AppFactory::create();
        $this->httpClient = new Client();

        // Получаем настройки ТОЛЬКО из переменных окружения
        $this->jwtSecret = getenv('JWT_SECRET');
        $this->jwtAlgorithm = getenv('JWT_ALGORITHM');

        // Валидация - обе переменные должны быть установлены
        if (empty($this->jwtSecret)) {
            throw new \RuntimeException('JWT_SECRET environment variable is not set');
        }
        if (empty($this->jwtAlgorithm)) {
            throw new \RuntimeException('JWT_ALGORITHM environment variable is not set');
        }

        // === CORS middleware ПЕРВЫМ ===
        $this->app->add(function (Request $request, $handler): Response {
            if ($request->getMethod() === 'OPTIONS') {
                $response = new \Slim\Psr7\Response();
                return $response
                    ->withHeader('Access-Control-Allow-Origin', '*')
                    ->withHeader('Access-Control-Allow-Headers', 'X-Requested-With, Content-Type, Accept, Origin, Authorization')
                    ->withHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, PATCH, OPTIONS')
                    ->withHeader('Access-Control-Allow-Credentials', 'true');
            }

            $response = $handler->handle($request);
            return $response
                ->withHeader('Access-Control-Allow-Origin', '*')
                ->withHeader('Access-Control-Allow-Headers', 'X-Requested-With, Content-Type, Accept, Origin, Authorization')
                ->withHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, PATCH, OPTIONS')
                ->withHeader('Access-Control-Allow-Credentials', 'true');
        });

        $this->setupMiddleware();
        $this->setupRoutes();
    }

    /**
     * Настройка middleware (настоящая JWT аутентификация)
     */
    private function setupMiddleware(): void
    {
        $this->app->add(function (Request $request, $handler): Response {
            $path = $request->getUri()->getPath();
            $method = $request->getMethod();

            // === ДЛЯ ОТЛАДКИ ===
            error_log("=== API GATEWAY AUTH ===");
            error_log("Path: {$path}");
            error_log("Method: {$method}");
            error_log("JWT Secret configured: " . (strlen($this->jwtSecret) > 0 ? 'YES' : 'NO'));
            error_log("JWT Algorithm: {$this->jwtAlgorithm}");

            // Публичные маршруты (без аутентификации)
            $publicRoutes = [
                '/health',
                '/',
                '/auth/test-login',
                '/auth/test-token',
                '/tts/voices',
                '/tts/validate-voice',
                // User Service публичные эндпоинты
                '/api/user/auth/request-otp',
                '/api/user/auth/verify-otp',
                '/api/user/auth/register',
                '/api/user/auth/login',
                // Public Workout Service - публичные тренировки
                '/public/workouts'
            ];

            // Проверяем, является ли маршрут публичным
            $isPublicRoute = false;
            
            // 1. Точные совпадения
            foreach ($publicRoutes as $publicPath) {
                if ($path === $publicPath) {
                    $isPublicRoute = true;
                    error_log("Route {$path} is PUBLIC (exact match: {$publicPath})");
                    break;
                }
            }
            
            // 2. Маршруты, начинающиеся с публичных префиксов
            if (!$isPublicRoute) {
                $publicPrefixes = [
                    '/public/workouts/',
                    '/api/user/auth/',
                    '/auth/',
                    '/tts/voices',
                    '/tts/validate-voice'
                ];
                
                foreach ($publicPrefixes as $prefix) {
                    if (strpos($path, $prefix) === 0) {
                        $isPublicRoute = true;
                        error_log("Route {$path} is PUBLIC (prefix: {$prefix})");
                        break;
                    }
                }
            }

            // Для публичных маршрутов пропускаем аутентификацию
            if ($isPublicRoute) {
                error_log("Skipping auth for public route");
                error_log("=== AUTH END (PUBLIC) ===");
                return $handler->handle($request);
            }

            error_log("Route {$path} is PRIVATE, checking JWT...");

            // Для приватных маршрутов проверяем JWT токен
            $authHeader = $request->getHeaderLine('Authorization');
            error_log("Auth header: " . ($authHeader ? $authHeader : 'MISSING'));

            if (empty($authHeader) || !preg_match('/Bearer\s+(.*)$/i', $authHeader, $matches)) {
                error_log("ERROR: No valid Authorization header");

                $error = EnsiErrorHandler::unauthorized(
                    'Missing or invalid Authorization header',
                    $path,
                    ['required_format' => 'Bearer {token}']
                );

                $response = new \Slim\Psr7\Response();
                $json = json_encode($error, JSON_THROW_ON_ERROR);
                $response->getBody()->write($json);

                error_log("=== AUTH END (401 NO TOKEN) ===");
                return $response
                    ->withStatus(401)
                    ->withHeader('Content-Type', 'application/json');
            }

            $token = $matches[1];
            error_log("Token extracted: " . substr($token, 0, 20) . "...");

            try {
                // === НАСТОЯЩАЯ JWT ВАЛИДАЦИЯ ===
                error_log("Validating JWT token...");
                error_log("Using JWT Secret from env: " . substr($this->jwtSecret, 0, 10) . "...");
                
                // Декодируем токен без проверки подписи сначала, чтобы посмотреть алгоритм
                $tks = explode('.', $token);
                if (count($tks) !== 3) {
                    throw new \Exception('Invalid token format');
                }

                // Получаем заголовок
                $headerRaw = JWT::urlsafeB64Decode($tks[0]);
                $header = json_decode($headerRaw, true);
                error_log("JWT Header: " . json_encode($header));
                
                // Используем алгоритм из заголовка токена или из переменной окружения
                $algorithm = $header['alg'] ?? $this->jwtAlgorithm;
                error_log("JWT Algorithm from header: " . ($header['alg'] ?? 'NOT SET'));
                error_log("Using algorithm: {$algorithm}");

                // Проверяем токен с секретным ключом
                $decoded = JWT::decode($token, new Key($this->jwtSecret, $algorithm));
                error_log("JWT Decoded successfully");

                // Извлекаем данные пользователя
                $userId = $decoded->sub ?? null;
                $email = $decoded->email ?? null;
                $role = $decoded->role ?? $decoded->{'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'} ?? 'Member';
                $name = $decoded->name ?? $decoded->{'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'} ?? 'Unknown';
                
                error_log("User ID: {$userId}");
                error_log("Email: {$email}");
                error_log("Role: {$role}");
                error_log("Name: {$name}");

                // Проверяем срок действия
                $currentTime = time();
                if (isset($decoded->exp) && $decoded->exp < $currentTime) {
                    throw new \Exception('Token expired');
                }

                // Добавляем данные пользователя в запрос
                $userData = [
                    'user_id' => $userId,
                    'email' => $email,
                    'role' => $role,
                    'name' => $name,
                    'token' => $token,
                    'jwt_payload' => (array)$decoded
                ];
                
                $request = $request->withAttribute('user', $userData);
                error_log("Auth SUCCESS for user: {$email} ({$userId})");

            } catch (\Exception $e) {
                error_log("JWT VALIDATION FAILED: " . $e->getMessage());

                $error = EnsiErrorHandler::unauthorized(
                    'Invalid or expired token',
                    $path,
                    ['error' => $e->getMessage()]
                );

                $response = new \Slim\Psr7\Response();
                $json = json_encode($error, JSON_THROW_ON_ERROR);
                $response->getBody()->write($json);

                error_log("=== AUTH END (401 INVALID TOKEN) ===");
                return $response
                    ->withStatus(401)
                    ->withHeader('Content-Type', 'application/json');
            }

            error_log("=== AUTH END (SUCCESS) ===");
            return $handler->handle($request);
        });
    }

    private function setupRoutes(): void
    {
        // ========== PUBLIC ROUTES ==========

        // Health check
        $this->app->get('/health', function (Request $request, Response $response): Response {
            $json = json_encode([
                'status' => 'OK',
                'service' => 'api-gateway',
                'timestamp' => date('Y-m-d H:i:s'),
                'jwt_auth' => 'enabled',
                'user_service' => 'integrated',
                'public_workout_service' => 'available',
                'environment_config' => 'from_env_variables'
            ], JSON_THROW_ON_ERROR);
            $response->getBody()->write($json);
            return $response->withHeader('Content-Type', 'application/json');
        });

        // Тестовый JWT токен (для разработки) - использует тот же секрет из env
        $this->app->post('/auth/test-token', function (Request $request, Response $response): Response {
            // Генерируем тестовый JWT токен с тем же секретом из env
            $payload = [
                'sub' => 'test-user-id-123',
                'email' => 'test@example.com',
                'name' => 'Test User',
                'role' => 'Member',
                'iat' => time(),
                'exp' => time() + 3600,
                'iss' => 'ReBuilder'
            ];
            
            $token = JWT::encode($payload, $this->jwtSecret, $this->jwtAlgorithm);
            
            $json = json_encode([
                'success' => true,
                'message' => 'Test JWT token for development',
                'accessToken' => $token,
                'expiresIn' => 3600,
                'tokenType' => 'Bearer',
                'note' => 'Use: Authorization: Bearer ' . $token,
                'algorithm' => $this->jwtAlgorithm
            ], JSON_THROW_ON_ERROR);
            $response->getBody()->write($json);
            return $response->withHeader('Content-Type', 'application/json');
        });

        // ========== USER SERVICE ==========
        // ИСПРАВЛЕНО: Используем /api/user (singular) вместо /api/users
        $this->app->any('/api/user[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            error_log("Proxying to User Service: /api/user{$path}");
            return $this->proxyToService($request, $response, "http://localhost:8004{$path}");
        });

        // ========== PUBLIC WORKOUT SERVICE ==========
        $this->app->any('/public/workouts[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            error_log("Proxying to Public Workout Service: /public/workouts{$path}");
            return $this->proxyToService($request, $response, "http://public-workout-service:80/public/workouts{$path}");
        });

        // ========== ТРЕНИРОВКИ (training-service) ==========
        
        // 1. Генерация озвучки для тренировок
        $this->app->any('/private/workouts/{workoutId}/tts[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $workoutId = $args['workoutId'] ?? '';
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            return $this->proxyToService($request, $response, "http://training-service:80/private/workouts/{$workoutId}/tts{$path}");
        });

        // 2. Приватные тренировки
        $this->app->any('/private/workouts[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            return $this->proxyToService($request, $response, "http://training-service:80/private/workouts{$path}");
        });

        // 3. Приватные упражнения
        $this->app->any('/private/exercises[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            return $this->proxyToService($request, $response, "http://training-service:80/private/exercises{$path}");
        });

        // 4. Тестовый маршрут db-test
        $this->app->any('/db-test[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            return $this->proxyToService($request, $response, "http://training-service:80/db-test{$path}");
        });

        // ========== TTS СЕРВИС ==========
        $this->app->any('/api/tts[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            error_log("Proxying to TTS Service: /api/tts{$path}");
            return $this->proxyToService($request, $response, "http://text-to-speech-service:80{$path}");
        });

        // ========== TRAINING SERVICE (публичный API) ==========
        $this->app->any('/api/training[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            error_log("Proxying to Training Service: /api/training{$path}");
            return $this->proxyToService($request, $response, "http://training-service:80{$path}");
        });

        // Корневой маршрут
        $this->app->get('/', function (Request $request, Response $response): Response {
            $json = json_encode([
                'message' => 'ReBuilder API Gateway',
                'version' => '1.0',
                'timestamp' => date('Y-m-d H:i:s'),
                'authentication' => 'JWT Bearer token',
                'configuration' => 'from_environment_variables',
                'active_endpoints' => [
                    '/health' => 'Health check (public)',
                    '/auth/test-token' => 'Get test JWT token (public)',
                    '/api/user/*' => 'User Service (public auth, private user data)',
                    '/public/workouts/*' => 'Public workouts (public)',
                    '/api/training/*' => 'Training service (public)',
                    '/api/tts/*' => 'Text-to-speech service',
                    '/private/workouts/*' => 'Private workouts (requires JWT)',
                    '/private/exercises/*' => 'Private exercises (requires JWT)'
                ]
            ], JSON_THROW_ON_ERROR);
            $response->getBody()->write($json);
            return $response->withHeader('Content-Type', 'application/json');
        });

        // Обработка OPTIONS для CORS
        $this->app->options('/{routes:.+}', function (Request $request, Response $response): Response {
            return $response
                ->withHeader('Access-Control-Allow-Origin', '*')
                ->withHeader('Access-Control-Allow-Headers', 'X-Requested-With, Content-Type, Accept, Origin, Authorization')
                ->withHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, PATCH, OPTIONS')
                ->withHeader('Access-Control-Allow-Credentials', 'true');
        });
    }

    private function proxyToService(Request $request, Response $response, string $serviceUrl): Response
    {
        try {
            error_log("=== API GATEWAY PROXY ===");
            error_log("Target: " . $serviceUrl);

            $user = $request->getAttribute('user', null);
            error_log("User: " . ($user ? json_encode($user) : 'No user (public route)'));

            $method = $request->getMethod();
            $query = $request->getUri()->getQuery();
            $targetUrl = $serviceUrl . ($query ? '?' . $query : '');

            $options = [
                'headers' => $request->getHeaders(),
                'timeout' => 30
            ];

            unset($options['headers']['Host']);
            unset($options['headers']['Content-Length']);

            // Передаем пользовательские данные сервисам
            if ($user && isset($user['user_id'])) {
                $options['headers']['X-User-Id'] = [$user['user_id']];
                $options['headers']['X-User-Role'] = [$user['role'] ?? 'Member'];
                $options['headers']['X-User-Email'] = [$user['email'] ?? ''];
                error_log("Adding user headers for: " . $user['user_id']);
            }

            // Передаем тело запроса
            $bodyContent = $request->getBody()->getContents();
            $request->getBody()->rewind();

            if (in_array($method, ['POST', 'PUT', 'PATCH', 'DELETE']) && !empty($bodyContent)) {
                $contentType = $request->getHeaderLine('Content-Type');
                error_log("Body type: " . $contentType);
                
                if (strstr($contentType, 'application/json')) {
                    $jsonData = json_decode($bodyContent, true);
                    if (json_last_error() === JSON_ERROR_NONE) {
                        $options['json'] = $jsonData;
                        error_log("Sending as JSON");
                    } else {
                        $options['body'] = $bodyContent;
                        $options['headers']['Content-Type'] = ['application/json'];
                        error_log("Sending as raw JSON string");
                    }
                } else {
                    $options['body'] = $bodyContent;
                }
            }

            error_log("Proxying: {$method} {$targetUrl}");
            $serviceResponse = $this->httpClient->request($method, $targetUrl, $options);

            $responseBody = $serviceResponse->getBody()->getContents();
            $response->getBody()->write($responseBody);

            error_log("Proxy SUCCESS: " . $serviceResponse->getStatusCode());
            return $response
                ->withStatus($serviceResponse->getStatusCode())
                ->withHeader('Content-Type', 'application/json');

        } catch (\GuzzleHttp\Exception\RequestException $e) {
            error_log("Proxy ERROR: " . $e->getMessage());
            error_log("Service: " . $serviceUrl);

            if ($e->hasResponse()) {
                $errorResponse = $e->getResponse();
                if ($errorResponse !== null) {
                    $response->getBody()->write($errorResponse->getBody()->getContents());
                    return $response
                        ->withStatus($errorResponse->getStatusCode())
                        ->withHeader('Content-Type', 'application/json');
                }
            }

            $json = json_encode([
                'error' => 'Service unavailable',
                'message' => $e->getMessage(),
                'service' => $serviceUrl,
                'timestamp' => date('Y-m-d H:i:s')
            ], JSON_THROW_ON_ERROR);
            $response->getBody()->write($json);
            
            return $response
                ->withStatus(503)
                ->withHeader('Content-Type', 'application/json');
        }
    }

    public function run(): void
    {
        $this->app->run();
    }
}