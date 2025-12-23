<?php

namespace Rebuilder\ApiGateway;

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

    public function __construct()
    {
        $this->app = AppFactory::create();
        $this->httpClient = new Client();

        // === ВАЖНО: Добавляем CORS middleware ПЕРВЫМ ===
        $this->app->add(function (Request $request, $handler): Response {
            // Обработка preflight OPTIONS запросов
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
     * Настройка middleware (аутентификация)
     */
    private function setupMiddleware(): void
    {
        // Middleware для проверки токенов
        $this->app->add(function (Request $request, $handler): Response {
            $path = $request->getUri()->getPath();
            $method = $request->getMethod();

            // === ЛОГИРОВАНИЕ ДЛЯ ОТЛАДКИ ===
            error_log("=== AUTH MIDDLEWARE START ===");
            error_log("Path: {$path}");
            error_log("Method: {$method}");
            error_log("Full URI: " . $request->getUri());

            // Публичные маршруты (без аутентификации)
            $publicRoutes = [
                '/health',
                '/',
                '/auth/test-login',
                '/tts/voices',
                '/tts/validate-voice'
            ];

            // Проверяем, является ли маршрут публичным
            $isPublicRoute = false;
            foreach ($publicRoutes as $publicPath) {
                // Точное совпадение или начало пути
                if ($path === $publicPath || strpos($path, $publicPath . '/') === 0) {
                    $isPublicRoute = true;
                    error_log("Route {$path} is PUBLIC (matches {$publicPath})");
                    break;
                }
            }

            // Для публичных маршрутов пропускаем аутентификацию
            if ($isPublicRoute) {
                error_log("Skipping auth for public route");
                error_log("=== AUTH MIDDLEWARE END (PUBLIC) ===");
                return $handler->handle($request);
            }

            error_log("Route {$path} is PRIVATE, checking auth...");

            // Для приватных маршрутов проверяем наличие токена
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
                try {
                    $json = json_encode($error, JSON_THROW_ON_ERROR);
                    $response->getBody()->write($json);
                } catch (\JsonException $e) {
                    // Fallback на простой JSON
                    $response->getBody()->write('{"error": "JSON encoding failed"}');
                }

                error_log("=== AUTH MIDDLEWARE END (401 NO TOKEN) ===");

                /** @var array{status?: int} $error */
                $statusCode = $error['status'] ?? 401;

                return $response
                    ->withStatus($statusCode)
                    ->withHeader('Content-Type', 'application/json');
            }

            $token = $matches[1];
            error_log("Token extracted: " . substr($token, 0, 20) . "...");

            // БАЗОВАЯ ПРОВЕРКА ТОКЕНА
            // Простая проверка формата токена
            if (strlen($token) < 10) {
                error_log("ERROR: Token too short: " . strlen($token));

                $error = EnsiErrorHandler::unauthorized(
                    'Invalid token format',
                    $path,
                    ['token_length' => strlen($token)]
                );

                $response = new \Slim\Psr7\Response();

                try {
                    $json = json_encode($error, JSON_THROW_ON_ERROR);
                    // Здесь PhpStan знает что $json - строка
                    $response->getBody()->write($json);
                } catch (\JsonException $e) {
                    // Обработка ошибки
                    $response->getBody()->write('{"error": "JSON encoding failed"}');
                }

                error_log("=== AUTH MIDDLEWARE END (401 SHORT TOKEN) ===");
                /** @var array{status?: int} $error */
                $statusCode = $error['status'] ?? 401;
                return $response
                    ->withStatus($statusCode)
                    ->withHeader('Content-Type', 'application/json');
            }

            // Добавляем заглушку пользователя для внутренних сервисов
            /** @var array{user_id: string, role: string, token: string} $userData */
            $userData = [
                'user_id' => '550e8400-e29b-41d4-a716-446655440000',
                'role' => 'user',
                'token' => $token
            ];
            $request = $request->withAttribute('user', $userData);

            error_log("Auth SUCCESS for user: 550e8400-e29b-41d4-a716-446655440000");
            error_log("=== AUTH MIDDLEWARE END (SUCCESS) ===");

            return $handler->handle($request);
        });
    }

    private function setupRoutes(): void
    {
        // ========== PUBLIC ROUTES ==========

        // Health check
        $this->app->get('/health', function (Request $request, Response $response): Response {
            try {
                $json = json_encode([
                    'status' => 'OK',
                    'service' => 'api-gateway',
                    'timestamp' => date('Y-m-d H:i:s'),
                    'authentication' => 'basic (token required for private routes)'
                ], JSON_THROW_ON_ERROR);
                /** @phpstan-ignore notIdentical.alwaysTrue */
                if ($json !== false) {
                    $response->getBody()->write($json);
                }
            } catch (\JsonException $e) {
                // Fallback на простой JSON
                $response->getBody()->write('{"error": "JSON encoding failed"}');
            }

            return $response->withHeader('Content-Type', 'application/json');
        });

        // Тестовый вход (для разработки без C# users-service)
        $this->app->post('/auth/test-login', function (Request $request, Response $response): Response {
            $json = json_encode([
                'success' => true,
                'message' => 'Use any token for testing. Real auth will be implemented with C# users-service.',
                'test_token' => 'test_jwt_token_for_development_' . uniqid(),
                'note' => 'Add Authorization: Bearer {token} header for private routes'
            ], JSON_THROW_ON_ERROR);
            /** @phpstan-ignore notIdentical.alwaysTrue */
            if ($json !== false) {
                $response->getBody()->write($json);
            }
            return $response->withHeader('Content-Type', 'application/json');
        });

        // ========== ТРЕНИРОВКИ (training-service) ==========
        // ВАЖНО: Специфичные маршруты должны быть ВЫШЕ общих

        // 1. Генерация озвучки для тренировок (специфичный маршрут)
        $this->app->any('/private/workouts/{workoutId}/tts[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $workoutId = $args['workoutId'] ?? '';
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            return $this->proxyToService($request, $response, "http://training-service:80/private/workouts/{$workoutId}/tts{$path}");
        });

        // 2. Приватные тренировки (общий маршрут - должен быть ПОСЛЕ специфичных)
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
        $this->app->any('/tts[/{params:.*}]', function (Request $request, Response $response, array $args): Response {
            $params = $args['params'] ?? '';
            $path = $params ? "/{$params}" : '';
            return $this->proxyToService($request, $response, "http://text-to-speech-service:80{$path}");
        });

        // Корневой маршрут
        $this->app->get('/', function (Request $request, Response $response): Response {
            $json = json_encode([
                'message' => 'ReBuilder API Gateway',
                'version' => '1.0',
                'timestamp' => date('Y-m-d H:i:s'),
                'authentication' => 'Basic token validation (Bearer token required for private routes)',
                'active_endpoints' => [
                    '/health' => 'Health check (public)',
                    '/auth/test-login' => 'Get test token (public)',
                    '/private/workouts/*' => 'Private workouts management (requires token)',
                    '/private/exercises/*' => 'Private exercises management (requires token)',
                    '/tts/*' => 'Text-to-speech service (requires token for /generate)'
                ],
                'notes' => 'Full authentication with C# users-service will be implemented later'
            ], JSON_THROW_ON_ERROR);
            /** @phpstan-ignore notIdentical.alwaysTrue */
            if ($json !== false) {
                $response->getBody()->write($json);
            }
            return $response->withHeader('Content-Type', 'application/json');
        });

        // Обработка OPTIONS для CORS (дублируем для ясности)
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
            // === ОТЛАДКА ===
            error_log("=== API GATEWAY PROXY ===");
            error_log("Method: " . $request->getMethod());
            error_log("Path: " . $request->getUri()->getPath());
            error_log("Target URL: " . $serviceUrl);

            /** @var array{user_id: string, role: string, token: string}|null $user */
            $user = $request->getAttribute('user', null);
            error_log("User attribute: " . json_encode($user));

            $method = $request->getMethod();
            $query = $request->getUri()->getQuery();

            // Формируем полный URL
            $targetUrl = $serviceUrl;
            if ($query) {
                $targetUrl .= '?' . $query;
            }

            // Подготавливаем опции для Guzzle
            $options = [
                'headers' => $request->getHeaders(),
                'timeout' => 30
            ];

            // Убираем заголовки, которые могут мешать
            unset($options['headers']['Host']);
            unset($options['headers']['Content-Length']);

            // Передаем пользовательские данные сервисам
            // Передаем пользовательские данные сервисам
            /** @phpstan-ignore function.alreadyNarrowedType */
            if (is_array($user) && array_key_exists('user_id', $user) /** @phpstan-ignore function.alreadyNarrowedType */ && array_key_exists('role', $user)) {
                $options['headers']['X-User-Id'] = [$user['user_id']];
                $options['headers']['X-User-Role'] = [$user['role']];
                error_log("Adding user headers: " . json_encode(['X-User-Id' => $user['user_id'], 'X-User-Role' => $user['role']]));
            }

            // Добавляем тело запроса
            $bodyContent = $request->getBody()->getContents();
            $request->getBody()->rewind(); // Важно: перематываем поток

            if (in_array($method, ['POST', 'PUT', 'PATCH', 'DELETE']) && !empty($bodyContent)) {
                $options['body'] = $bodyContent;
                if (empty($options['headers']['Content-Type'])) {
                    $options['headers']['Content-Type'] = ['application/json'];
                }
            }

            // Проксируем запрос
            error_log("Proxying to: {$method} {$targetUrl}");
            $serviceResponse = $this->httpClient->request($method, $targetUrl, $options);

            // Возвращаем ответ от сервиса
            $responseBody = $serviceResponse->getBody()->getContents();
            $response->getBody()->write($responseBody);

            error_log("Proxy SUCCESS: Status " . $serviceResponse->getStatusCode());
            return $response
                ->withStatus($serviceResponse->getStatusCode())
                ->withHeader('Content-Type', 'application/json');

        } catch (\GuzzleHttp\Exception\RequestException $e) {
            error_log("API Gateway Error: " . $e->getMessage());
            error_log("Service URL: " . $serviceUrl);

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
            /** @phpstan-ignore notIdentical.alwaysTrue */
            if ($json !== false) {
                $response->getBody()->write($json);
            }
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
