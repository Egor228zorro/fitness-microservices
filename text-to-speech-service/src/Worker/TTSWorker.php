<?php

declare(strict_types=1);

namespace Rebuilder\TextToSpeech\Worker;

require_once __DIR__ . '/../../vendor/autoload.php';
require_once __DIR__ . '/../Database/TTSDatabaseConnection.php';

use GuzzleHttp\Client;
use GuzzleHttp\Exception\RequestException;
use PhpAmqpLib\Channel\AMQPChannel;
use PhpAmqpLib\Connection\AMQPStreamConnection;

class TTSWorker
{
    /** @var AMQPStreamConnection|null */
    private $connection = null;

    /** @var AMQPChannel|null */
    private $channel = null;

    private string $murfApiKey;
    private string $storageBaseUrl;

    public function __construct()
    {
        try {
            echo "=== TTS Worker Constructor ===\n";
            echo "Connecting to RabbitMQ at rabbitmq:5672...\n";

            // RabbitMQ соединение
            $this->connection = new AMQPStreamConnection(
                'rabbitmq',
                5672,
                'guest',
                'guest',
                '/',
                false,
                'AMQPLAIN',
                null,
                'en_US',
                10.0
            );

            $this->channel = $this->connection->channel();
            $this->channel->queue_declare('tts_tasks', false, true, false, false);

            // Настройки
            $this->murfApiKey = (string) (getenv('MURF_API_KEY') ?: '');
            $this->storageBaseUrl = (string) (getenv('STORAGE_BASE_URL') ?: 'https://storage.rebuilder.app/audio');

            if (empty($this->murfApiKey)) {
                echo " [⚠] WARNING: MURF_API_KEY not set. Will use mock TTS only.\n";
                echo " [ℹ] To use real TTS, set MURF_API_KEY in .env or docker-compose.yml\n";
            } else {
                echo " [✓] Murf.ai API key loaded\n";
            }

            echo " [✓] RabbitMQ connected successfully\n";
            echo " [✓] Storage base URL: {$this->storageBaseUrl}\n";
            echo " [*] TTS Worker started. Waiting for tasks...\n";

        } catch (\Exception $e) {
            echo " [!] FATAL ERROR in constructor: " . $e->getMessage() . "\n";
            echo "Stack trace: " . $e->getTraceAsString() . "\n";
            error_log("TTS Worker FATAL: " . $e->getMessage());
            exit(1);
        }
    }

    /**
     * Вызов внутреннего TTS API для синтеза речи
     * @return array{success: bool, audio_url: string, mock: bool, job_id?: string, status_code?: int, error?: string}
     */
    private function callInternalTtsApi(string $text, string $voiceId, string $jobId): array
    {
        try {
            echo " [🌐] Calling INTERNAL TTS Service API...\n";
            echo "     Job ID: {$jobId}\n";
            echo "     Voice: {$voiceId}\n";
            echo "     Text length: " . strlen($text) . " chars\n";

            // Ограничение длины текста
            $maxLength = 5000;
            if (strlen($text) > $maxLength) {
                echo " [⚠] Text too long (" . strlen($text) . " chars), truncating to {$maxLength}\n";
                $text = substr($text, 0, $maxLength) . '... [truncated]';
            }

            // Вызываем наш собственный сервис
            $client = new Client([
                'base_uri' => 'http://text-to-speech-service:80',
                'timeout' => 60,
                'headers' => [
                    'Content-Type' => 'application/json',
                    'Accept' => 'application/json',
                ],
            ]);

            $payload = [
                'text' => $text,
                'voice_id' => $voiceId,
                'job_id' => $jobId,
            ];

            echo " [📤] Sending request to internal service: /internal/generate-sync\n";

            $response = $client->post('/internal/generate-sync', [
                'json' => $payload,
                'http_errors' => false,
            ]);

            $statusCode = $response->getStatusCode();
            $body = $response->getBody()->getContents();

            echo " [🔍] Internal service response: HTTP {$statusCode}\n";
            echo " [🔍] Response body length: " . strlen($body) . " bytes\n";

            $bodyPreview = substr($body, 0, 500);
            if (strlen($body) > 500) {
                $bodyPreview .= '...';
            }
            echo " [🔍] Response preview: {$bodyPreview}\n";

            /** @var array<string, mixed> $result */
            $result = json_decode($body, true);

            if (json_last_error() !== JSON_ERROR_NONE) {
                echo " [✗] JSON decode error: " . json_last_error_msg() . "\n";
                echo " [✗] Raw body: " . $body . "\n";

                return [
                    'success' => false,
                    'audio_url' => $this->storageBaseUrl . '/json-error/' . uniqid('json_error_', true) . '.mp3',
                    'mock' => true,
                    'error' => 'JSON decode error: ' . json_last_error_msg(),
                    'status_code' => $statusCode
                ];
            }

            if ($statusCode === 200) {
                if (isset($result['audio_url']) && is_string($result['audio_url'])) {
                    echo " [✓] Internal TTS API success (audio_url found)\n";
                    echo "     Audio URL: {$result['audio_url']}\n";
                    $mock = isset($result['mock']) ? (bool) $result['mock'] : false;
                    echo "     Mock: " . ($mock ? 'YES' : 'NO') . "\n";

                    return [
                        'success' => isset($result['success']) ? (bool) $result['success'] : true,
                        'audio_url' => $result['audio_url'],
                        'mock' => $mock,
                        'job_id' => $jobId,
                        'status_code' => $statusCode
                    ];
                } elseif (isset($result['audioUrl']) && is_string($result['audioUrl'])) {
                    echo " [✓] Internal TTS API success (audioUrl found)\n";
                    echo "     Audio URL: {$result['audioUrl']}\n";

                    return [
                        'success' => isset($result['success']) ? (bool) $result['success'] : true,
                        'audio_url' => $result['audioUrl'],
                        'mock' => isset($result['mock']) ? (bool) $result['mock'] : false,
                        'job_id' => $jobId,
                        'status_code' => $statusCode
                    ];
                } elseif (isset($result['audioFile']) && is_string($result['audioFile'])) {
                    echo " [✓] Internal TTS API success (audioFile found)\n";
                    echo "     Audio URL: {$result['audioFile']}\n";

                    return [
                        'success' => isset($result['success']) ? (bool) $result['success'] : true,
                        'audio_url' => $result['audioFile'],
                        'mock' => isset($result['mock']) ? (bool) $result['mock'] : false,
                        'job_id' => $jobId,
                        'status_code' => $statusCode
                    ];
                } else {
                    $errorMsg = '';
                    if (isset($result['error']) && is_string($result['error'])) {
                        $errorMsg = $result['error'];
                    } elseif (isset($result['message']) && is_string($result['message'])) {
                        $errorMsg = $result['message'];
                    } else {
                        $errorMsg = 'Success but no audio URL found';
                    }

                    echo " [⚠] Success response but no audio URL: {$errorMsg}\n";
                    echo " [🔍] Response keys: " . implode(', ', array_keys($result)) . "\n";
                }
            }

            $errorMsg = '';
            if (isset($result['error']) && is_string($result['error'])) {
                $errorMsg = $result['error'];
            } elseif (isset($result['message']) && is_string($result['message'])) {
                $errorMsg = $result['message'];
            } else {
                $errorMsg = 'Unknown internal service error';
            }

            echo " [✗] Internal service error (HTTP {$statusCode}): {$errorMsg}\n";

            return [
                'success' => false,
                'audio_url' => $this->storageBaseUrl . '/fallback/' . uniqid('fallback_', true) . '.mp3',
                'mock' => true,
                'error' => "Internal service error {$statusCode}: " . $errorMsg,
                'status_code' => $statusCode
            ];

        } catch (RequestException $e) {
            echo " [✗] Internal service HTTP error: " . $e->getMessage() . "\n";

            return [
                'success' => false,
                'audio_url' => $this->storageBaseUrl . '/error/' . uniqid('error_', true) . '.mp3',
                'mock' => true,
                'error' => 'HTTP Request failed: ' . $e->getMessage()
            ];
        } catch (\Exception $e) {
            echo " [✗] Internal service general error: " . $e->getMessage() . "\n";

            return [
                'success' => false,
                'audio_url' => $this->storageBaseUrl . '/error/' . uniqid('error_', true) . '.mp3',
                'mock' => true,
                'error' => 'General error: ' . $e->getMessage()
            ];
        }
    }

    /**
     * Генерация текста для озвучки на основе данных тренировки
     * @param array<string, mixed> $workoutData
     * @param array<array<string, mixed>> $exercisesData
     */
    private function generateTtsText(array $workoutData, array $exercisesData): string
    {
        echo " [📝] Generating TTS text from workout data...\n";

        $name = $workoutData['name'] ?? 'Без названия';
        $text = "Начинаем тренировку. " . (is_string($name) ? $name : 'Без названия') . ". ";

        if (!empty($workoutData['type']) && is_string($workoutData['type'])) {
            $text .= "Тип тренировки: " . $workoutData['type'] . ". ";
        }

        $text .= "Упражнений: " . count($exercisesData) . ". ";

        foreach ($exercisesData as $index => $exercise) {
            $exerciseNumber = $index + 1;
            $exerciseName = $exercise['name'] ?? 'Без названия';
            $text .= "Упражнение {$exerciseNumber}: " . (is_string($exerciseName) ? $exerciseName : 'Без названия') . ". ";

            if (!empty($exercise['description']) && is_string($exercise['description'])) {
                $text .= $exercise['description'] . ". ";
            }

            if (!empty($exercise['duration_seconds']) && is_numeric($exercise['duration_seconds'])) {
                $duration = (int) $exercise['duration_seconds'];
                $minutes = floor($duration / 60);
                $seconds = $duration % 60;
                $text .= "Длительность: ";
                if ($minutes > 0) {
                    $text .= "{$minutes} минут ";
                }
                if ($seconds > 0) {
                    $text .= "{$seconds} секунд. ";
                }
            }

            if (!empty($exercise['rest_seconds']) && is_numeric($exercise['rest_seconds'])) {
                $text .= "Отдых: " . (int) $exercise['rest_seconds'] . " секунд. ";
            }
        }

        $text .= "Тренировка завершена. Хорошей работы!";

        echo "     Generated text length: " . strlen($text) . " chars\n";
        return $text;
    }

    public function run(): void
    {
        if (!$this->channel) {
            echo " [!] RabbitMQ channel not available\n";
            return;
        }

        $callback = function ($msg) {
            /** @var mixed $rawData */
            $rawData = json_decode($msg->body, true);

            // Проверяем что декодирование прошло успешно
            if (!is_array($rawData)) {
                echo " [!] Invalid message format\n";
                $msg->ack();
                return;
            }

            /** @var array{
             *     job_id?: mixed,
             *     text?: mixed,
             *     voice_id?: mixed,
             *     workout_id?: mixed,
             *     workout_data?: mixed,
             *     exercises_data?: mixed
             * } $data */
            $data = $rawData;

            // Получаем значения с проверкой типов
            $jobId = isset($data['job_id']) && is_string($data['job_id']) ? $data['job_id'] : '';
            $text = isset($data['text']) && is_string($data['text']) ? $data['text'] : '';
            $voiceId = isset($data['voice_id']) && is_string($data['voice_id']) ? $data['voice_id'] : 'en-US-alina';

            $workoutId = null;
            if (isset($data['workout_id']) && is_string($data['workout_id'])) {
                $workoutId = $data['workout_id'];
            }

            $workoutData = [];
            if (isset($data['workout_data']) && is_array($data['workout_data'])) {
                $workoutData = $data['workout_data'];
            }

            $exercisesData = [];
            if (isset($data['exercises_data']) && is_array($data['exercises_data'])) {
                $exercisesData = $data['exercises_data'];
            }

            // Проверяем что обязательные поля не пустые
            if (empty($jobId)) {
                echo " [!] Empty job_id\n";
                $msg->ack();
                return;
            }

            echo "\n" . str_repeat("=", 60) . "\n";
            echo " [x] Processing job: {$jobId}\n";
            echo "     Workout ID: " . ($workoutId ?? 'null') . "\n";
            echo "     Voice: {$voiceId}\n";
            echo "     Queue message ID: " . $msg->getDeliveryTag() . "\n";

            try {
                $db = \Rebuilder\TextToSpeech\Database\TTSDatabaseConnection::getInstance()->getConnection();
                echo " [✓] Database connected\n";
            } catch (\Exception $e) {
                echo " [✗] Database connection failed: " . $e->getMessage() . "\n";
                return;
            }

            try {
                $checkStmt = $db->prepare("SELECT status FROM tts_jobs WHERE job_id = ?");
                $checkStmt->execute([$jobId]);
                /** @var array{status?: string}|false $existingJob */
                $existingJob = $checkStmt->fetch();

                if ($existingJob !== false &&
                    isset($existingJob['status']) &&
                    in_array($existingJob['status'], ['completed', 'processing', 'failed'], true)) {
                    echo " [⚠] Job already processed with status: {$existingJob['status']}\n";
                    $msg->ack();
                    return;
                }

                $stmt = $db->prepare("
                    INSERT INTO tts_jobs (job_id, workout_id, language, voice_profile, payload, status) 
                    VALUES (:job_id, :workout_id, :language, :voice_profile, :payload, :status)
                    ON CONFLICT (job_id) 
                    DO UPDATE SET 
                        status = EXCLUDED.status,
                        updated_at = CURRENT_TIMESTAMP
                ");

                $payloadData = [
                    'text' => $text,
                    'voice_id' => $voiceId,
                    'original_length' => strlen($text),
                    'workout_id' => $workoutId,
                    'received_from_queue' => true,
                    'workout_data' => $workoutData,
                    'exercises_count' => count($exercisesData)
                ];

                $stmt->execute([
                    ':job_id' => $jobId,
                    ':workout_id' => $workoutId ?? '00000000-0000-0000-0000-000000000000',
                    ':language' => 'ru-RU',
                    ':voice_profile' => $voiceId,
                    ':payload' => json_encode($payloadData, JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE),
                    ':status' => 'processing'
                ]);

                echo " [✓] Job status updated to 'processing'\n";

                if (empty($text) && !empty($workoutData) && !empty($exercisesData)) {
                    echo " [🔄] Generating TTS text from workout data...\n";
                    /** @var array<array<string, mixed>> $exercisesData */
                    $text = $this->generateTtsText($workoutData, $exercisesData);
                    echo "     Generated text preview: " . substr($text, 0, 100) . "...\n";
                }

                if (empty($text)) {
                    throw new \Exception("No text provided for TTS synthesis");
                }

                $startTime = microtime(true);
                $ttsResult = $this->callInternalTtsApi($text, $voiceId, $jobId);
                $processingTime = round(microtime(true) - $startTime, 2);

                echo " [⏱] TTS processing time: {$processingTime} seconds\n";

                $updateData = [
                    'status' => $ttsResult['success'] ? 'completed' : 'failed',
                    'result_url' => $ttsResult['audio_url'],
                    'error_message' => $ttsResult['error'] ?? null,
                    'job_id' => $jobId
                ];

                $stmt = $db->prepare("
                    UPDATE tts_jobs 
                    SET status = :status,
                        result_url = :result_url,
                        error_message = :error_message,
                        updated_at = CURRENT_TIMESTAMP,
                        payload = jsonb_set(
                            COALESCE(payload::jsonb, '{}'::jsonb), 
                            '{tts_result}', 
                            to_jsonb(:tts_result::jsonb)
                        )
                    WHERE job_id = :job_id
                ");

                $ttsResultForDb = [
                    'success' => $ttsResult['success'],
                    'mock' => $ttsResult['mock'],  // УБРАЛ `?? false`
                    'processing_time' => $processingTime,
                    'text_length' => strlen($text),
                    'timestamp' => date('Y-m-d H:i:s'),
                    'internal_service_called' => true,
                    'status_code' => $ttsResult['status_code'] ?? null
                ];

                $stmt->execute(array_merge($updateData, [
                    ':tts_result' => json_encode($ttsResultForDb, JSON_THROW_ON_ERROR)
                ]));

                if ($ttsResult['success']) {
                    echo " [✅] Job completed successfully: {$jobId}\n";
                    echo "     Audio URL: {$ttsResult['audio_url']}\n";
                    echo "     Mock: " . ($ttsResult['mock'] ? 'YES' : 'NO') . "\n";
                } else {
                    echo " [❌] Job failed: {$jobId}\n";
                    $error = $ttsResult['error'] ?? 'Unknown error';
                    echo "     Error: " . $error . "\n";
                }

                try {
                    $archiveStmt = $db->prepare("
                        INSERT INTO tts_messages_archive 
                        (queue_name, message_id, message_body, processed_at, job_id)
                        VALUES (?, ?, ?, CURRENT_TIMESTAMP, ?)
                    ");
                    $archiveStmt->execute([
                        'tts_tasks',
                        $msg->getDeliveryTag(),
                        $msg->body,
                        $jobId
                    ]);
                    echo " [📚] Message archived\n";
                } catch (\Exception $archiveError) {
                    echo " [⚠] Archive failed: " . $archiveError->getMessage() . "\n";
                }

                $msg->ack();
                echo " [✓] Message acknowledged and removed from queue\n";

            } catch (\Exception $e) {
                try {
                    $stmt = $db->prepare("
                        UPDATE tts_jobs 
                        SET status = 'failed', 
                            error_message = ?,
                            updated_at = CURRENT_TIMESTAMP 
                        WHERE job_id = ?
                    ");
                    $stmt->execute([$e->getMessage(), $jobId]);
                    echo " [💀] Job marked as failed: {$jobId}\n";
                    echo "     Error: " . $e->getMessage() . "\n";
                } catch (\Exception $updateError) {
                    echo " [⚠] Failed to update error status: " . $updateError->getMessage() . "\n";
                }

                echo " [✗] Job processing error: " . $e->getMessage() . "\n";
                echo "Stack trace:\n" . $e->getTraceAsString() . "\n";

                echo " [⚠] Message NOT acknowledged - will stay in queue for retry\n";
            }

            echo str_repeat("=", 60) . "\n\n";
        };

        try {
            $this->channel->basic_qos(0, 1, false);  // 0 вместо null, false вместо null

            $this->channel->basic_consume(
                'tts_tasks',
                '',
                false,
                false,
                false,
                false,
                $callback
            );

            echo " [✓] Waiting for messages in queue 'tts_tasks'...\n";
            echo " [✓] Manual acknowledgement enabled\n";
            echo " [✓] Prefetch count: 1 (process one message at a time)\n";
            echo " [✓] Messages flow:\n";
            echo "     1. Receive from RabbitMQ\n";
            echo "     2. Update DB status to 'processing'\n";
            echo "     3. Call INTERNAL TTS Service API (/internal/generate-sync)\n";
            echo "     4. Update DB with result\n";
            echo "     5. Archive message\n";
            echo "     6. Acknowledge (remove from queue)\n";
            echo "\n" . str_repeat("-", 60) . "\n";

            while ($this->channel->is_consuming()) {
                $this->channel->wait();
            }

        } catch (\Exception $e) {
            echo " [!] Error in main loop: " . $e->getMessage() . "\n";
            error_log("TTS Worker loop error: " . $e->getMessage());
        } finally {
            try {
                // Явно проверяем на null, хотя PhpStan знает что не null
                if ($this->channel !== null) {
                    $this->channel->close();
                }
                if ($this->connection !== null) {
                    $this->connection->close();
                }
                echo " [✓] RabbitMQ connection closed\n";
            } catch (\Exception $e) {
                // Игнорируем ошибки закрытия
            }

        }
    }
}

// Запуск воркера
if (php_sapi_name() === 'cli') {
    echo "=== TTS Worker ===\n";
    echo "Starting at: " . date('Y-m-d H:i:s') . "\n";
    echo "PHP Version: " . PHP_VERSION . "\n";
    echo "PID: " . getmypid() . "\n";
    echo "Murf.ai API: " . (getenv('MURF_API_KEY') ? 'Configured' : 'Not configured (mock mode)') . "\n";
    echo "Internal service endpoint: /internal/generate-sync\n\n";

    try {
        $worker = new TTSWorker();
        $worker->run();
    } catch (\Exception $e) {
        echo " [!] Global error: " . $e->getMessage() . "\n";
        error_log("TTS Worker global error: " . $e->getMessage());
        exit(1);
    }
}
