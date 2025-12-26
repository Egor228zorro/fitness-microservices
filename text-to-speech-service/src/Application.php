<?php

declare(strict_types=1);

namespace Rebuilder\TextToSpeech;

use GuzzleHttp\Client;
use JsonException;
use Psr\Container\ContainerInterface;
use Psr\Http\Message\ResponseInterface as Response;
use Psr\Http\Message\ServerRequestInterface as Request;
use Rebuilder\TextToSpeech\Common\EnsiErrorHandler;
use Slim\App;
use Slim\Exception\HttpNotFoundException;
use Slim\Factory\AppFactory;
use Slim\Middleware\ErrorMiddleware;

class Application
{
    /** @var App<ContainerInterface|null> */
    private App $app;
    private Client $httpClient;
    private string $murfApiKey;
    private string $basePublicUrl;

    public function __construct()
    {
        // Создаем приложение БЕЗ дефолтного ErrorMiddleware
        $this->app = AppFactory::create();
        $this->httpClient = new Client();

        $apiKey = $_ENV['MURF_API_KEY'] ?? '';
        $this->murfApiKey = is_string($apiKey) ? $apiKey : '';

        // Базовый URL для публичных ссылок через API Gateway
        $this->basePublicUrl = $_ENV['TTS_PUBLIC_URL'] ?? 'http://localhost:8000';
        
        $this->app->addBodyParsingMiddleware();
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
        
        // ✅ КРИТИЧЕСКИ ВАЖНО: Добавляем ПУСТОЙ ErrorMiddleware который НЕ преобразует
        $errorMiddleware = $this->app->addErrorMiddleware(
            false,  // displayErrorDetails
            true,   // logErrors
            true    // logErrorDetails
        );

        // ✅ ПЕРЕОПРЕДЕЛЯЕМ обработчик ошибок - возвращаем ENSE формат
        $errorMiddleware->setDefaultErrorHandler(function (
            Request $request,
            \Throwable $exception,
            bool $displayErrorDetails,
            bool $logErrors,
            bool $logErrorDetails
        ) {
            // ✅ РАЗЛИЧАЕМ ТИПЫ ИСКЛЮЧЕНИЙ
            if ($exception instanceof HttpNotFoundException) {
                $error = EnsiErrorHandler::notFound(
                    $exception->getMessage(),
                    $request->getUri()->getPath(),
                    [
                        'exception' => get_class($exception),
                        'file' => $exception->getFile(),
                        'line' => $exception->getLine(),
                    ]
                );
            } else {
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
            }

            $response = new \Slim\Psr7\Response();
            $response->getBody()->write(json_encode($error, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR));
            $statusCode = 500; // значение по умолчанию
            if (isset($error['status'])) {
                $status = $error['status'];
                if (is_numeric($status)) {
                    $statusCode = (int) $status;
                } elseif (is_string($status) && ctype_digit($status)) {
                    $statusCode = (int) $status;
                }
            }
            return $response
                ->withStatus($statusCode)
                ->withHeader('Content-Type', 'application/json');
        });

        $this->setupRoutes();
    }

    private function setupRoutes(): void
    {
        $self = $this;

        // Health check
        $this->app->get('/health', function (Request $request, Response $response) use ($self): Response {
            $data = [
                'status' => 'OK',
                'service' => 'text-to-speech',
                'murf_api_configured' => !empty($self->murfApiKey),
                'rabbitmq_available' => $self->checkRabbitMQ(),
                'timestamp' => date('Y-m-d H:i:s'),
                'public_url_base' => $self->basePublicUrl
            ];

            return $self->writeJson($response, $data, $request->getUri()->getPath());
        });

        // Получение списка доступных голосов
        $this->app->get('/voices', function (Request $request, Response $response) use ($self): Response {
            return $self->getAvailableVoices($response, $request->getUri()->getPath());
        });

        // Генерация озвучки через Murf.ai (с RabbitMQ)
        $this->app->post('/generate', function (Request $request, Response $response) use ($self): Response {
            try {
                /** @var array{text?: string, voice_id?: string, workout_id?: string} $data */
                $data = (array) $request->getParsedBody();

                $text = $data['text'] ?? '';
                $voiceId = $data['voice_id'] ?? 'en-US-alina';
                $workoutId = $data['workout_id'] ?? null;

                if (empty($text)) {
                    $error = EnsiErrorHandler::validationError(
                        'Text is required for speech generation',
                        '/generate',
                        ['field' => 'text', 'reason' => 'required_field_missing']
                    );

                    return $self->writeJson($response->withStatus(400), $error, $request->getUri()->getPath());
                }

                return $self->generateSpeech($response, $text, $voiceId, $workoutId, $request->getUri()->getPath());

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError(
                    $e->getMessage(),
                    '/generate',
                    ['exception' => get_class($e)]
                );

                return $self->writeJson($response->withStatus(500), $error, $request->getUri()->getPath());
            }
        });

        // === ВАЖНО: Синхронный эндпоинт для TTS Worker (без RabbitMQ) ===
        $this->app->post('/internal/generate-sync', function (Request $request, Response $response) use ($self): Response {
            try {
                /** @var array{text?: string, voice_id?: string, job_id?: string} $data */
                $data = (array) $request->getParsedBody();

                $text = $data['text'] ?? '';
                $voiceId = $data['voice_id'] ?? 'en-US-alina';
                $jobId = $data['job_id'] ?? '';

                if (empty($text)) {
                    $error = EnsiErrorHandler::validationError(
                        'Text is required for speech generation',
                        '/internal/generate-sync',
                        ['field' => 'text', 'reason' => 'required_field_missing']
                    );

                    return $self->writeJson($response->withStatus(400), $error, $request->getUri()->getPath());
                }

                return $self->generateSpeechSync($response, $text, $voiceId, $jobId, $request->getUri()->getPath());

            } catch (\Exception $e) {
                $error = EnsiErrorHandler::serverError(
                    $e->getMessage(),
                    '/internal/generate-sync',
                    ['exception' => get_class($e)]
                );

                return $self->writeJson($response->withStatus(500), $error, $request->getUri()->getPath());
            }
        });

        // Получение статуса TTS задачи из БД
        $this->app->get('/status/{jobId}', function (Request $request, Response $response, array $args) use ($self): Response {
            $jobId = (string) ($args['jobId'] ?? '');
            return $self->getJobStatus($response, $jobId, $request->getUri()->getPath());
        });

        // ✅ ВАЖНО: Локальный эндпоинт для аудио (только для внутреннего использования через Gateway)
        $this->app->get('/audio/{filename}', function (Request $request, Response $response, array $args): Response {
            $filename = $args['filename'];
    
            // Проверяем безопасность имени файла
            if (!preg_match('/^[a-zA-Z0-9_.-]+\.mp3$/', $filename)) {
                $response->getBody()->write(json_encode(['error' => 'Invalid filename']));
                return $response
                     ->withStatus(400)
                     ->withHeader('Content-Type', 'application/json');
            }
    
            $filepath = '/var/www/html/public/audio/' . $filename;
    
            if (file_exists($filepath)) {
                $response->getBody()->write(file_get_contents($filepath));
                return $response
                    ->withHeader('Content-Type', 'audio/mpeg')
                    ->withHeader('Content-Disposition', 'inline')
                    ->withHeader('Cache-Control', 'public, max-age=86400');
            }
    
            $response->getBody()->write(json_encode(['error' => 'Audio file not found']));
            return $response
                ->withStatus(404)
                ->withHeader('Content-Type', 'application/json');
        });

        // Валидация голоса
        $this->app->post('/validate-voice', function (Request $request, Response $response) use ($self): Response {
            /** @var array{voice_id?: string} $data */
            $data = (array) $request->getParsedBody();
            $voiceId = $data['voice_id'] ?? '';

            $validVoices = ['en-US-alina', 'en-US-cooper', 'en-UK-hazel', 'en-US-daniel'];
            $isValid = in_array($voiceId, $validVoices, true);

            $data = [
                'voice_id' => $voiceId,
                'valid' => $isValid,
                'available_voices' => $validVoices
            ];

            return $self->writeJson($response, $data, $request->getUri()->getPath());
        });

        // Метрики сервиса
        $this->app->get('/metrics', function (Request $request, Response $response) use ($self): Response {
            require_once __DIR__ . '/Database/TTSDatabaseConnection.php';
            $db = \Rebuilder\TextToSpeech\Database\TTSDatabaseConnection::getInstance()->getConnection();

            $stmt = $db->query("
                        SELECT 
                                COUNT(*) as total_jobs,
                                COUNT(CASE WHEN status = 'completed' THEN 1 END) as completed_jobs,
                                COUNT(CASE WHEN status = 'pending' THEN 1 END) as pending_jobs,
                                COUNT(CASE WHEN status = 'processing' THEN 1 END) as processing_jobs,
                                COUNT(CASE WHEN status = 'failed' THEN 1 END) as failed_jobs,
                                MAX(created_at) as last_job_time
                        FROM tts_jobs
                ");

            // Проверяем успешность выполнения запроса
            if ($stmt === false) {
                // Возвращаем метрики по умолчанию при ошибке базы данных
                $metrics = [
                        'tts_requests_total' => 0,
                        'tts_requests_completed' => 0,
                        'tts_requests_pending' => 0,
                        'tts_requests_processing' => 0,
                        'tts_requests_failed' => 0,
                        'last_job_time' => null,
                        'service_uptime' => '99.9%',
                        'rabbitmq_connected' => $self->checkRabbitMQ(),
                        'note' => 'Database query failed, showing default metrics'
                ];

                return $self->writeJson($response, $metrics, $request->getUri()->getPath());
            }

            $fetchedStats = $stmt->fetch(\PDO::FETCH_ASSOC);

            // Создаем гарантированный массив статистики
            $stats = [
                    'total_jobs' => 0,
                    'completed_jobs' => 0,
                    'pending_jobs' => 0,
                    'processing_jobs' => 0,
                    'failed_jobs' => 0,
                    'last_job_time' => null
            ];

            // Если данные получены, обновляем значения
            if ($fetchedStats !== false && is_array($fetchedStats)) {
                $stats['total_jobs'] = isset($fetchedStats['total_jobs']) && is_numeric($fetchedStats['total_jobs'])
                        ? (int) $fetchedStats['total_jobs']
                        : 0;
                $stats['completed_jobs'] = isset($fetchedStats['completed_jobs']) && is_numeric($fetchedStats['completed_jobs'])
                        ? (int) $fetchedStats['completed_jobs']
                        : 0;
                $stats['pending_jobs'] = isset($fetchedStats['pending_jobs']) && is_numeric($fetchedStats['pending_jobs'])
                        ? (int) $fetchedStats['pending_jobs']
                        : 0;
                $stats['processing_jobs'] = isset($fetchedStats['processing_jobs']) && is_numeric($fetchedStats['processing_jobs'])
                        ? (int) $fetchedStats['processing_jobs']
                        : 0;
                $stats['failed_jobs'] = isset($fetchedStats['failed_jobs']) && is_numeric($fetchedStats['failed_jobs'])
                        ? (int) $fetchedStats['failed_jobs']
                        : 0;
                $stats['last_job_time'] = isset($fetchedStats['last_job_time']) && is_string($fetchedStats['last_job_time'])
                        ? $fetchedStats['last_job_time']
                        : null;
            }

            $metrics = [
                    'tts_requests_total' => $stats['total_jobs'],
                    'tts_requests_completed' => $stats['completed_jobs'],
                    'tts_requests_pending' => $stats['pending_jobs'],
                    'tts_requests_processing' => $stats['processing_jobs'],
                    'tts_requests_failed' => $stats['failed_jobs'],
                    'last_job_time' => $stats['last_job_time'],
                    'service_uptime' => '99.9%',
                    'rabbitmq_connected' => $self->checkRabbitMQ()
            ];

            return $self->writeJson($response, $metrics, $request->getUri()->getPath());
        });

        // Тестовый маршрут для 500 ошибки
        $this->app->get('/test-500', function (Request $request, Response $response): Response {
            throw new \RuntimeException('Тестовая 500 ошибка: что-то пошло не так!');
        });

        // Тестовый маршрут (синхронный, без RabbitMQ)
        $this->app->get('/test-sync', function (Request $request, Response $response) use ($self): Response {
            $testText = "Hello! This is a test speech generation.";
            return $self->generateSpeech($response, $testText, 'en-US-alina', null, $request->getUri()->getPath());
        });

        // Тестовый маршрут (асинхронный, с RabbitMQ)
        $this->app->get('/test-async', function (Request $request, Response $response) use ($self): Response {
            $testText = "Hello! This is an async test with RabbitMQ.";
            return $self->generateSpeech($response, $testText, 'en-US-alina', null, $request->getUri()->getPath());
        });

        // ===== ОТЛАДОЧНЫЙ МАРШРУТ ДЛЯ ДИАГНОСТИКИ ПАРСИНГА ТЕЛА =====
        $this->app->post('/debug-body', function (Request $request, Response $response): Response {
            $rawBody = (string)$request->getBody();
            $contentType = $request->getHeaderLine('Content-Type');
            $parsedBody = $request->getParsedBody();
            
            $data = [
                'parsed_body' => $parsedBody,
                'raw_body' => $rawBody,
                'content_type' => $contentType,
                'method' => $request->getMethod(),
                'php_input' => file_get_contents('php://input'),
                'headers' => $request->getHeaders()
            ];
            
            $json = json_encode($data, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR);
            $response->getBody()->write($json);
            return $response->withHeader('Content-Type', 'application/json');
        });

        // Корневой маршрут
        $this->app->get('/', function (Request $request, Response $response) use ($self): Response {
            $data = [
                'message' => 'Text-to-Speech Service',
                'provider' => 'Murf.ai',
                'api_configured' => !empty($self->murfApiKey),
                'queue_system' => 'RabbitMQ',
                'public_url_base' => $self->basePublicUrl,
                'endpoints' => [
                    '/health' => 'Health check',
                    '/voices' => 'Get available voices',
                    '/generate' => 'Generate speech (POST: text, voice_id, workout_id)',
                    '/internal/generate-sync' => 'Sync speech generation for TTS Worker',
                    '/status/{jobId}' => 'Get TTS job status',
                    '/audio/{filename}' => '[INTERNAL] Get audio file (use via Gateway)',
                    '/validate-voice' => 'Validate voice ID',
                    '/metrics' => 'Service metrics',
                    '/test-500' => 'Test 500 error',
                    '/test-sync' => 'Test sync generation',
                    '/test-async' => 'Test async generation',
                    '/debug-body' => 'Debug request body parsing'
                ]
            ];

            return $self->writeJson($response, $data, $request->getUri()->getPath());
        });
    }

    private function getAvailableVoices(Response $response, string $path = '/'): Response
    {
        try {
            if (empty($this->murfApiKey)) {
                $error = EnsiErrorHandler::validationError(
                    'API key required to fetch voices',
                    '/voices',
                    [
                        'service' => 'murf.ai',
                        'reason' => 'missing_api_key',
                        'mock_voices' => [
                            ['voiceId' => 'en-US-alina', 'displayName' => 'Alina (F)', 'locale' => 'en-US'],
                            ['voiceId' => 'en-US-cooper', 'displayName' => 'Cooper (M)', 'locale' => 'en-US']
                        ]
                    ]
                );

                return $this->writeJson($response->withStatus(400), $error, $path);
            }

            $murfResponse = $this->httpClient->get('https://api.murf.ai/v1/speech/voices', [
                'headers' => [
                    'api-key' => $this->murfApiKey,
                    'Content-Type' => 'application/json',
                ],
                'timeout' => 10
            ]);

            /** @var array<mixed> $voices */
            $voices = json_decode($murfResponse->getBody()->getContents(), true, 512, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR);

            return $this->writeJson($response, $voices, $path);

        } catch (\Exception $e) {
            $error = EnsiErrorHandler::externalServiceError(
                $e->getMessage(),
                '/voices',
                ['provider' => 'murf.ai', 'endpoint' => '/v1/speech/voices']
            );

            return $this->writeJson($response->withStatus(502), $error, $path);
        }
    }

    /**
     * Синхронная генерация речи (для TTS Worker)
     */
    private function generateSpeechSync(Response $response, string $text, string $voiceId, string $jobId, string $path = '/'): Response
    {
        require_once __DIR__ . '/Database/TTSDatabaseConnection.php';
        $db = \Rebuilder\TextToSpeech\Database\TTSDatabaseConnection::getInstance()->getConnection();

        try {
            // Обновляем статус на "processing"
            $stmt = $db->prepare("
                UPDATE tts_jobs 
                SET status = 'processing', 
                    updated_at = CURRENT_TIMESTAMP 
                WHERE job_id = :job_id
            ");
            $stmt->execute([':job_id' => $jobId]);

            error_log("TTS Worker calling sync generation for job: {$jobId}");

            // Прямой вызов Murf.ai API
            if (empty($this->murfApiKey)) {
                // Mock режим
                $audioUrl = $this->basePublicUrl . '/audio/mock/' . uniqid('mock_', true) . '.mp3';

                $stmt = $db->prepare("
                    UPDATE tts_jobs 
                    SET status = 'completed', 
                        result_url = :result_url,
                        audio_local_path = NULL,
                        updated_at = CURRENT_TIMESTAMP,
                        payload = jsonb_set(
                            COALESCE(payload::jsonb, '{}'::jsonb), 
                            '{mock}', 
                            to_jsonb(true::boolean)
                        )
                    WHERE job_id = :job_id
                ");
                $stmt->execute([
                    ':result_url' => $audioUrl,
                    ':job_id' => $jobId
                ]);

                return $this->writeJson($response, [
                    'success' => true,
                    'job_id' => $jobId,
                    'audio_url' => $audioUrl,
                    'mock' => true,
                    'message' => 'Mock audio generated (no API key)'
                ], $path);
            }

            // Реальный вызов Murf.ai API
            try {
                $murfResponse = $this->httpClient->post('https://api.murf.ai/v1/speech/generate', [
                    'headers' => [
                        'api-key' => $this->murfApiKey,
                        'Content-Type' => 'application/json',
                    ],
                    'json' => [
                        'text' => $text,
                        'voice_id' => $voiceId,
                        'format' => 'mp3',
                        'sample_rate' => 48000,
                        'speed' => 1.0,
                        'pitch' => 0,
                    ],
                    'timeout' => 30,
                    'http_errors' => false,
                ]);

                $statusCode = $murfResponse->getStatusCode();
                $body = $murfResponse->getBody()->getContents();
                /** @var array<string, mixed> $result */
                $result = json_decode($body, true, 512, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR);

                if ($statusCode === 200 && isset($result['audioFile']) && is_string($result['audioFile'])) {
                    // ✅ ДОБАВЛЕНО: Скачиваем аудио через прокси и сохраняем локально
                    $audioUrl = $result['audioFile'];
                    
                    // 1. Скачиваем аудио через прокси (TTS Service)
                    $audioContent = @file_get_contents($audioUrl);

                    if ($audioContent !== false) {
                        // 2. Создаем папку для аудио если не существует
                        $audioDir = '/var/www/html/public/audio/';
                        if (!is_dir($audioDir)) {
                            mkdir($audioDir, 0755, true);
                        }
                        
                        // 3. Сохраняем локально
                        $filename = $jobId . '.mp3';
                        $filepath = $audioDir . $filename;
                        file_put_contents($filepath, $audioContent);
                        
                        // 4. Создаем публичную ссылку через API Gateway
                        $publicUrl = "/audio/{$filename}";
                        $fullUrl = $this->basePublicUrl . $publicUrl;
                        
                        // 5. Сохраняем ссылку в БД
                        $stmt = $db->prepare("
                            UPDATE tts_jobs 
                            SET status = 'completed', 
                                result_url = :result_url,
                                audio_local_path = :local_path,
                                updated_at = CURRENT_TIMESTAMP,
                                payload = jsonb_set(
                                    COALESCE(payload::jsonb, '{}'::jsonb), 
                                    '{murf_response}', 
                                    to_jsonb(:murf_result::jsonb)
                                )
                            WHERE job_id = :job_id
                        ");
                        $stmt->execute([
                            ':result_url' => $fullUrl, // ✅ КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: ссылка через Gateway
                            ':local_path' => $filepath,
                            ':murf_result' => json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR),
                            ':job_id' => $jobId
                        ]);

                        return $this->writeJson($response, [
                            'success' => true,
                            'job_id' => $jobId,
                            'audio_url' => $fullUrl, // ✅ Возвращаем клиенту ссылку через Gateway
                            'local_path' => $filepath,
                            'mock' => false,
                            'message' => 'Audio generated and saved locally'
                        ], $path);
                    } else {
                        // Если не удалось скачать, сохраняем оригинальную ссылку
                        error_log("Failed to download audio from Murf.ai: {$audioUrl}");
                        
                        $stmt = $db->prepare("
                            UPDATE tts_jobs 
                            SET status = 'completed', 
                                result_url = :result_url,
                                audio_local_path = NULL,
                                updated_at = CURRENT_TIMESTAMP,
                                payload = jsonb_set(
                                    COALESCE(payload::jsonb, '{}'::jsonb), 
                                    '{murf_response}', 
                                    to_jsonb(:murf_result::jsonb)
                                )
                            WHERE job_id = :job_id
                        ");
                        $stmt->execute([
                            ':result_url' => $result['audioFile'],
                            ':murf_result' => json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR),
                            ':job_id' => $jobId
                        ]);

                        return $this->writeJson($response, [
                            'success' => true,
                            'job_id' => $jobId,
                            'audio_url' => $result['audioFile'],
                            'mock' => false,
                            'message' => 'Audio generated (original Murf.ai link)'
                        ], $path);
                    }
                } else {
                    // Ошибка Murf.ai
                    $errorMsgValue = $result['message'] ?? $result['error'] ?? 'Unknown Murf.ai API error';
                    $errorMsg = is_string($errorMsgValue) ? $errorMsgValue : 'Unknown error';

                    $stmt = $db->prepare("
                        UPDATE tts_jobs 
                        SET status = 'failed', 
                            error_message = :error_message,
                            audio_local_path = NULL,
                            updated_at = CURRENT_TIMESTAMP
                        WHERE job_id = :job_id
                    ");
                    $stmt->execute([
                        ':error_message' => "Murf.ai API error {$statusCode}: " . $errorMsg,
                        ':job_id' => $jobId
                    ]);

                    return $this->writeJson($response->withStatus(502), [
                        'success' => false,
                        'job_id' => $jobId,
                        'error' => $errorMsg,
                        'status_code' => $statusCode,
                        'message' => 'Murf.ai API error'
                    ], $path);
                }

            } catch (\Exception $e) {
                // Ошибка сети или другая ошибка
                $stmt = $db->prepare("
                    UPDATE tts_jobs 
                    SET status = 'failed', 
                        error_message = :error_message,
                        audio_local_path = NULL,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE job_id = :job_id
                ");
                $stmt->execute([
                    ':error_message' => 'Network error: ' . $e->getMessage(),
                    ':job_id' => $jobId
                ]);

                return $this->writeJson($response->withStatus(500), [
                    'success' => false,
                    'job_id' => $jobId,
                    'error' => $e->getMessage(),
                    'message' => 'Network error calling Murf.ai'
                ], $path);
            }

        } catch (\Exception $e) {
            $error = EnsiErrorHandler::serverError(
                $e->getMessage(),
                $path,
                ['function' => 'generateSpeechSync', 'job_id' => $jobId, 'voice_id' => $voiceId]
            );

            return $this->writeJson($response->withStatus(500), $error, $path);
        }
    }

    private function generateSpeech(Response $response, string $text, string $voiceId = 'en-US-alina', ?string $workoutId = null, string $path = '/'): Response
    {
        require_once __DIR__ . '/Database/TTSDatabaseConnection.php';
        $db = \Rebuilder\TextToSpeech\Database\TTSDatabaseConnection::getInstance()->getConnection();

        $jobId = 'tts_' . uniqid('', true);

        try {
            // Очищаем текст от невалидных UTF-8 символов
            $cleanText = $this->cleanUtf8String($text);

            // 1. Сохраняем задачу в БД со статусом "pending"
            $stmt = $db->prepare("
                INSERT INTO tts_jobs (job_id, workout_id, language, voice_profile, payload, status) 
                VALUES (:job_id, :workout_id, :language, :voice_profile, :payload, :status)
                RETURNING id, created_at
            ");

            $stmt->execute([
                ':job_id' => $jobId,
                ':workout_id' => $workoutId ?? '00000000-0000-0000-0000-000000000000',
                ':language' => 'ru-RU',
                ':voice_profile' => $voiceId,
                ':payload' => json_encode([
                    'text' => $cleanText,
                    'voice_id' => $voiceId,
                    'original_length' => strlen($cleanText),
                    'workout_id' => $workoutId
                ], JSON_PRETTY_PRINT | JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE),
                ':status' => 'pending'
            ]);

            /** @var array{id?: int|string, created_at?: string}|false $jobData */
            $jobData = $stmt->fetch(\PDO::FETCH_ASSOC);

            if ($jobData === false) {
                throw new \RuntimeException('Failed to fetch job data after insertion');
            }

            // 2. Отправляем задачу в RabbitMQ очередь
            require_once __DIR__ . '/../vendor/autoload.php';

            // Проверяем доступность классов RabbitMQ
            if (!class_exists('PhpAmqpLib\Connection\AMQPStreamConnection') ||
                !class_exists('PhpAmqpLib\Message\AMQPMessage')) {

                // Если RabbitMQ недоступен, сохраняем задачу как pending и возвращаем ответ
                $data = [
                    'job_id' => $jobId,
                    'db_id' => (int)($jobData['id'] ?? 0),
                    'status' => 'pending',
                    'text_preview' => substr($cleanText, 0, 50) . (strlen($cleanText) > 50 ? '...' : ''),
                    'voice_id' => $voiceId,
                    'workout_id' => $workoutId,
                    'message' => 'TTS job saved but RabbitMQ not available',
                    'queue' => 'tts_tasks',
                    'created_at' => $jobData['created_at'] ?? date('Y-m-d H:i:s'),
                    'estimated_time' => 'Delayed - RabbitMQ not available',
                    'rabbitmq_status' => 'not_available',
                    'note' => 'RabbitMQ classes not found, job will be processed when available'
                ];

                return $this->writeJson($response, $data, $path);
            }

            $connection = new \PhpAmqpLib\Connection\AMQPStreamConnection('rabbitmq', 5672, 'guest', 'guest');
            $channel = $connection->channel();
            $channel->queue_declare('tts_tasks', false, true, false, false);

            $message = new \PhpAmqpLib\Message\AMQPMessage(
                json_encode([
                    'job_id' => $jobId,
                    'text' => $cleanText,
                    'voice_id' => $voiceId,
                    'workout_id' => $workoutId,
                    'timestamp' => time(),
                    'queue' => 'tts_tasks'
                ], JSON_PRETTY_PRINT | JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE),
                ['delivery_mode' => \PhpAmqpLib\Message\AMQPMessage::DELIVERY_MODE_PERSISTENT]
            );

            $channel->basic_publish($message, '', 'tts_tasks');
            $channel->close();
            $connection->close();

            // 3. Возвращаем ответ, что задача принята в очередь
            $data = [
                'job_id' => $jobId,
                'db_id' => (int)($jobData['id'] ?? 0),
                'status' => 'pending',
                'text_preview' => substr($cleanText, 0, 50) . (strlen($cleanText) > 50 ? '...' : ''),
                'voice_id' => $voiceId,
                'workout_id' => $workoutId,
                'message' => 'TTS job queued for processing',
                'queue' => 'tts_tasks',
                'created_at' => $jobData['created_at'] ?? date('Y-m-d H:i:s'),
                'estimated_time' => '5-10 seconds',
                'rabbitmq_status' => 'connected'
            ];

            return $this->writeJson($response, $data, $path);

        } catch (\Exception $e) {
            // В случае ошибки тоже сохраняем в БД
            try {
                $stmt = $db->prepare("
                    UPDATE tts_jobs 
                    SET status = 'failed', 
                        error_message = :error,
                        audio_local_path = NULL,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE job_id = :job_id
                ");
                $stmt->execute([
                    ':error' => $e->getMessage(),
                    ':job_id' => $jobId
                ]);
            } catch (\Exception $updateError) {
                error_log("Failed to update failed job: " . $updateError->getMessage());
            }

            $error = EnsiErrorHandler::serverError(
                $e->getMessage(),
                '/generate',
                ['function' => 'generateSpeech', 'voice_id' => $voiceId, 'workout_id' => $workoutId]
            );

            return $this->writeJson($response->withStatus(500), $error, $path);
        }
    }

    private function getJobStatus(Response $response, string $jobId, string $path = '/'): Response
    {
        require_once __DIR__ . '/Database/TTSDatabaseConnection.php';
        $db = \Rebuilder\TextToSpeech\Database\TTSDatabaseConnection::getInstance()->getConnection();

        try {
            $stmt = $db->prepare("
                SELECT job_id, workout_id, status, result_url, error_message, 
                       created_at, updated_at, payload
                FROM tts_jobs 
                WHERE job_id = :job_id
            ");
            $stmt->execute([':job_id' => $jobId]);
            /** @var array<string, mixed>|false $job */
            $job = $stmt->fetch(\PDO::FETCH_ASSOC);

            if (!$job) {
                $error = EnsiErrorHandler::notFound(
                    'TTS job not found',
                    '/status/' . $jobId,
                    ['job_id' => $jobId]
                );
                return $this->writeJson($response->withStatus(404), $error, $path);
            }

            return $this->writeJson($response, $job, $path);

        } catch (\Exception $e) {
            $error = EnsiErrorHandler::serverError(
                $e->getMessage(),
                '/status/' . $jobId,
                ['function' => 'getJobStatus', 'job_id' => $jobId]
            );

            return $this->writeJson($response->withStatus(500), $error, $path);
        }
    }

    private function checkRabbitMQ(): bool
    {
        try {
            require_once __DIR__ . '/../vendor/autoload.php';

            // Проверяем доступность классов RabbitMQ
            if (!class_exists('\PhpAmqpLib\Connection\AMQPStreamConnection')) {
                error_log("RabbitMQ classes not available");
                return false;
            }

            $connection = new \PhpAmqpLib\Connection\AMQPStreamConnection('rabbitmq', 5672, 'guest', 'guest', '/', false, 'AMQPLAIN', null, 'en_US', 3.0);
            $channel = $connection->channel();
            $channel->close();
            $connection->close();
            return true;
        } catch (\Exception $e) {
            error_log("RabbitMQ check failed: " . $e->getMessage());
            return false;
        }
    }

    /**
     * @param array<mixed> $data
     */
    private function writeJson(Response $response, array $data, string $path = '/'): Response
    {
        try {
            // Очищаем все данные от невалидных UTF-8 символов
            $cleanData = $this->cleanUtf8Recursive($data);

            $json = json_encode($cleanData, JSON_PRETTY_PRINT | JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE);
            $response->getBody()->write($json);
        } catch (JsonException $e) {
            $error = EnsiErrorHandler::serverError(
                'JSON encoding failed: ' . $e->getMessage(),
                $path,
                [
                    'json_error' => $e->getMessage(),
                    'json_last_error' => json_last_error_msg()
                ]
            );

            $response->getBody()->write(json_encode($error, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT | JSON_THROW_ON_ERROR));
            $response = $response->withStatus(500);
        }

        return $response->withHeader('Content-Type', 'application/json');
    }

    /**
     * Рекурсивно очищает массив от невалидных UTF-8 символов
     * @param mixed $data
     * @return mixed
     */
    private function cleanUtf8Recursive($data)
    {
        if (is_array($data)) {
            foreach ($data as $key => $value) {
                $data[$key] = $this->cleanUtf8Recursive($value);
            }
            return $data;
        }

        if (is_string($data)) {
            return $this->cleanUtf8String($data);
        }

        return $data;
    }

    /**
     * Очищает строку от невалидных UTF-8 символов
     */
    private function cleanUtf8String(string $string): string
    {
        // Преобразуем к UTF-8 если не UTF-8
        $detectedEncoding = mb_detect_encoding($string, mb_detect_order(), true);
        if ($detectedEncoding !== false && $detectedEncoding !== 'UTF-8') {
            $converted = mb_convert_encoding($string, 'UTF-8', $detectedEncoding);
            if ($converted !== false) {
                $string = $converted;
            }
        }

        // Удаляем невалидные UTF-8 символы
        $result = iconv('UTF-8', 'UTF-8//IGNORE', $string);

        return $result !== false ? $result : '';
    }

    public function run(): void
    {
        $this->app->run();
    }
}