<?php

declare(strict_types=1);

namespace Rebuilder\Training\Service;

use GuzzleHttp\Client;
use GuzzleHttp\Exception\RequestException;

class TtsClient
{
    private Client $client;

    public function __construct()
    {
        $this->client = new Client([
            "base_uri" => "http://text-to-speech-service:80",
            "timeout" => 10.0,
            "headers" => [
                "Content-Type" => "application/json",
                "Accept" => "application/json"
            ]
        ]);
    }

    public function sendTtsJob(string $text, string $workoutId, string $voiceId = 'en-US-alina'): string
    {
        try {
            // Исправляем кодировку текста
            $cleanText = mb_convert_encoding($text, "UTF-8", "UTF-8");

            $response = $this->client->post("/generate", [
                "json" => [
                    "text" => $cleanText,
                    "workout_id" => $workoutId,
                    "voice_id" => $voiceId
                ]
            ]);

            $body = $response->getBody()->getContents();
            /** @var array<string, mixed>|null $data */
            $data = json_decode($body, true);

            // Проверяем, что $data - массив и содержит 'job_id'
            if (!is_array($data) || !isset($data["job_id"])) {
                throw new \RuntimeException("TTS service did not return job_id");
            }

            $jobId = is_scalar($data["job_id"]) ? (string)$data["job_id"] : '';
            if ($jobId === '') {
                throw new \RuntimeException("Invalid job_id format");
            }

            error_log("TTS job sent via HTTP: " . $jobId . " for workout " . $workoutId);

            return $jobId;

        } catch (\Exception $e) {
            error_log("TtsClient HTTP ERROR: " . $e->getMessage());
            throw new \RuntimeException("Failed to send TTS job via HTTP: " . $e->getMessage());
        }
    }

    /**
     * Получить статус задачи TTS
     *
     * @param string $jobId ID задачи TTS
     * @return array<string, mixed> Статус задачи
     */
    public function getTtsJobStatus(string $jobId): array
    {
        try {
            $response = $this->client->get("/status/{$jobId}", [
                'http_errors' => false
            ]);

            $statusCode = $response->getStatusCode();

            if ($statusCode === 200) {
                $body = $response->getBody()->getContents();
                /** @var array<string, mixed>|null $data */
                $data = json_decode($body, true);

                // Гарантируем возврат массива
                return is_array($data) ? $data : [
                    'success' => false,
                    'job_id' => $jobId,
                    'status' => 'error',
                    'message' => 'Invalid response format from TTS service'
                ];
            }

            return [
                'success' => false,
                'job_id' => $jobId,
                'status' => 'error',
                'message' => "Failed to get TTS status, HTTP {$statusCode}"
            ];

        } catch (RequestException $e) {
            error_log("TtsClient get status ERROR: " . $e->getMessage());

            return [
                'success' => false,
                'job_id' => $jobId,
                'status' => 'error',
                'message' => 'Failed to connect to TTS service: ' . $e->getMessage()
            ];
        } catch (\Exception $e) {
            error_log("TtsClient get status ERROR: " . $e->getMessage());

            return [
                'success' => false,
                'job_id' => $jobId,
                'status' => 'error',
                'message' => 'Unexpected error: ' . $e->getMessage()
            ];
        }
    }
}
