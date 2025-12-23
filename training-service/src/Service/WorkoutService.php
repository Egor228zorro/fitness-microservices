<?php

declare(strict_types=1);

namespace Rebuilder\Training\Service;

use PDO;
use PDOStatement;
use Rebuilder\Training\Database\DatabaseConnection;

class WorkoutService
{
    private PDO $db;

    public function __construct()
    {
        $this->db = DatabaseConnection::getInstance()->getConnection();
    }

    /**
     * @param array{name?: string, type?: string} $data
     * @return array<string, mixed>
     */
    public function createWorkout(array $data): array
    {
        $userId = '550e8400-e29b-41d4-a716-446655440000';
        $name = $data['name'] ?? 'Новая тренировка';
        $type = $data['type'] ?? 'strength';

        // Валидация
        if (empty($name)) {
            throw new \InvalidArgumentException('Workout name cannot be empty');
        }

        if (!in_array($type, ['strength', 'cardio', 'flexibility'])) {
            throw new \InvalidArgumentException('Invalid workout type');
        }

        $stmt = $this->db->prepare("
            INSERT INTO workouts (user_id, name, type) 
            VALUES (:user_id, :name, :type) 
            RETURNING *
        ");

        if (!$stmt instanceof PDOStatement) {
            throw new \RuntimeException('Failed to prepare statement');
        }

        $result = $stmt->execute([
            'user_id' => $userId,
            'name' => $name,
            'type' => $type
        ]);

        if ($result === false) {
            throw new \RuntimeException('Failed to execute query');
        }

        /** @var array<string, mixed>|false $workout */
        $workout = $stmt->fetch(PDO::FETCH_ASSOC);

        if ($workout === false) {
            throw new \RuntimeException('Failed to create workout');
        }

        return $workout;
    }

    /**
     * @return array<int, array<string, mixed>>
     */
    public function getWorkouts(): array
    {
        $stmt = $this->db->query("SELECT * FROM workouts ORDER BY created_at DESC");
        if (!$stmt instanceof PDOStatement) {
            throw new \RuntimeException('Database query failed');
        }

        /** @var array<int, array<string, mixed>>|false $workouts */
        $workouts = $stmt->fetchAll(PDO::FETCH_ASSOC);

        if ($workouts === false) {
            throw new \RuntimeException('Failed to fetch workouts');
        }

        return $workouts;
    }

    /**
     * @return array<string, mixed>|null
     */
    public function getWorkoutById(string $id): ?array
    {
        $stmt = $this->db->prepare("SELECT * FROM workouts WHERE id = :id");
        if (!$stmt instanceof PDOStatement) {
            return null;
        }

        $result = $stmt->execute(['id' => $id]);
        if ($result === false) {
            return null;
        }

        /** @var array<string, mixed>|false $workout */
        $workout = $stmt->fetch(PDO::FETCH_ASSOC);

        return $workout !== false ? $workout : null;
    }

    /**
     * Получить тренировку по ID с проверкой пользователя
     * @param string $workoutId
     * @param string $userId
     * @return array<string, mixed>|null
     */
    public function getWorkoutByIdForUser(string $workoutId, string $userId): ?array
    {
        $stmt = $this->db->prepare("SELECT * FROM workouts WHERE id = :id AND user_id = :user_id");
        if (!$stmt instanceof PDOStatement) {
            return null;
        }

        $result = $stmt->execute(['id' => $workoutId, 'user_id' => $userId]);
        if ($result === false) {
            return null;
        }

        /** @var array<string, mixed>|false $workout */
        $workout = $stmt->fetch(PDO::FETCH_ASSOC);

        return $workout !== false ? $workout : null;
    }

    /**
     * Генерация TTS для тренировки
     *
     * @param string $workoutId UUID тренировки
     * @param string $voiceId ID голоса (по умолчанию 'en-US-alina')
     * @return string Job ID задачи TTS
     * @throws \InvalidArgumentException Если тренировка не найдена
     * @throws \RuntimeException При ошибках базы данных или RabbitMQ
     */
    public function generateTtsForWorkout(string $workoutId, string $voiceId = 'en-US-alina'): string
    {
        $userId = '550e8400-e29b-41d4-a716-446655440000';

        // 1. Получаем тренировку с упражнениями
        $stmt = $this->db->prepare("
            SELECT w.*, 
                   json_agg(
                       json_build_object(
                           'id', e.id,
                           'name', e.name,
                           'description', e.description,
                           'order_index', we.order_index,
                           'duration_seconds', we.duration_seconds,
                           'rest_seconds', we.rest_seconds
                       ) ORDER BY we.order_index
                   ) as exercises
            FROM workouts w
            LEFT JOIN workout_exercises we ON w.id = we.workout_id
            LEFT JOIN exercises e ON we.exercise_id = e.id
            WHERE w.id = :id AND w.user_id = :user_id
            GROUP BY w.id
        ");

        if (!$stmt instanceof PDOStatement) {
            throw new \RuntimeException('Failed to prepare SQL statement');
        }

        $result = $stmt->execute([
            'id' => $workoutId,
            'user_id' => $userId
        ]);

        if ($result === false) {
            throw new \RuntimeException('Failed to execute query');
        }

        /** @var array<string, mixed>|false $workout */
        $workout = $stmt->fetch(PDO::FETCH_ASSOC);

        if ($workout === false) {
            throw new \InvalidArgumentException("Workout not found: {$workoutId}");
        }

        // 2. Формируем текст для TTS
        $exercisesJson = $workout['exercises'] ?? '[]';
        $exercises = is_string($exercisesJson) ? json_decode($exercisesJson, true) : [];
        /** @var array<int, array<string, mixed>> $exercises */
        $exercises = is_array($exercises) ? $exercises : [];

        $workoutName = is_string($workout['name'] ?? null) ? $workout['name'] : 'Без названия';
        $ttsText = "Тренировка: " . $workoutName . ". ";

        if (!empty($exercises)) {
            $ttsText .= "Упражнения: ";
            foreach ($exercises as $index => $exercise) {
                /** @var array<string, mixed> $exercise */
                $exerciseName = is_string($exercise['name'] ?? null) ? $exercise['name'] : 'Без названия';
                $ttsText .= ($index + 1) . ". " . $exerciseName . ". ";

                $description = is_string($exercise['description'] ?? null) ? $exercise['description'] : '';
                if (!empty($description)) {
                    $ttsText .= $description . ". ";
                }

                $durationSeconds = is_numeric($exercise['duration_seconds'] ?? null) ? (int)$exercise['duration_seconds'] : 0;
                if ($durationSeconds > 0) {
                    $minutes = floor($durationSeconds / 60);
                    $seconds = $durationSeconds % 60;
                    if ($minutes > 0) {
                        $ttsText .= "Длительность: {$minutes} минут " . ($seconds > 0 ? "{$seconds} секунд" : "") . ". ";
                    } else {
                        $ttsText .= "Длительность: {$seconds} секунд. ";
                    }
                }

                $restSeconds = is_numeric($exercise['rest_seconds'] ?? null) ? (int)$exercise['rest_seconds'] : 0;
                if ($restSeconds > 0) {
                    $ttsText .= "Отдых: {$restSeconds} секунд. ";
                }
            }
        } else {
            $ttsText .= "Нет упражнений в тренировке.";
        }

        // 3. Отправляем в TTS сервис через TtsClient
        $ttsClient = new TtsClient();
        $jobId = $ttsClient->sendTtsJob($ttsText, $workoutId, $voiceId);

        // 4. Сохраняем job_id в workouts
        try {
            $updateStmt = $this->db->prepare("UPDATE workouts SET tts_job_id = :job_id, updated_at = CURRENT_TIMESTAMP WHERE id = :id");
            if ($updateStmt instanceof PDOStatement) {
                $updateStmt->execute(['job_id' => $jobId, 'id' => $workoutId]);
            }
        } catch (\Exception $e) {
            error_log("Note: tts_job_id field might not exist in workouts table: " . $e->getMessage());
        }

        return $jobId;
    }

    /**
     * Получить статус TTS для тренировки
     *
     * @param string $workoutId UUID тренировки
     * @return array<string, mixed> Статус TTS
     */
    public function getTtsStatus(string $workoutId): array
    {
        try {
            // 1. Получаем тренировку
            $workout = $this->getWorkoutById($workoutId);

            if (!$workout) {
                return [
                    'success' => false,
                    'workout_id' => $workoutId,
                    'status' => 'not_found',
                    'message' => 'Workout not found'
                ];
            }

            // 2. Проверяем, есть ли job_id
            $jobId = is_string($workout['tts_job_id'] ?? null) ? $workout['tts_job_id'] : null;

            if (!$jobId) {
                return [
                    'success' => false,
                    'workout_id' => $workoutId,
                    'status' => 'not_started',
                    'message' => 'TTS generation not started for this workout'
                ];
            }

            // 3. Запрашиваем статус через TtsClient
            $ttsClient = new TtsClient();
            $status = $ttsClient->getTtsJobStatus($jobId);

            /** @var array<string, mixed> $status */
            $workoutName = is_string($workout['name'] ?? null) ? $workout['name'] : 'Unknown';

            return [
                'success' => true,
                'workout_id' => $workoutId,
                'workout_name' => $workoutName,
                'tts_job_id' => $jobId,
                'status' => is_string($status['status'] ?? null) ? $status['status'] : 'unknown',
                'audio_url' => $status['result_url'] ?? null,
                'error_message' => $status['error_message'] ?? null,
                'created_at' => $status['created_at'] ?? null,
                'updated_at' => $status['updated_at'] ?? null
            ];

        } catch (\Exception $e) {
            return [
                'success' => false,
                'workout_id' => $workoutId,
                'status' => 'error',
                'message' => 'Failed to get TTS status: ' . $e->getMessage(),
                'error' => $e->getMessage()
            ];
        }
    }

    /**
     * Получить тренировку с упражнениями
     *
     * @param string $workoutId UUID тренировки
     * @return array<string, mixed>|null
     */
    public function getWorkoutWithExercises(string $workoutId): ?array
    {
        $stmt = $this->db->prepare("
            SELECT w.*, 
                   json_agg(
                       json_build_object(
                           'id', e.id,
                           'name', e.name,
                           'description', e.description,
                           'order_index', we.order_index,
                           'duration_seconds', we.duration_seconds,
                           'rest_seconds', we.rest_seconds
                       ) ORDER BY we.order_index
                   ) as exercises
            FROM workouts w
            LEFT JOIN workout_exercises we ON w.id = we.workout_id
            LEFT JOIN exercises e ON we.exercise_id = e.id
            WHERE w.id = :id
            GROUP BY w.id
        ");

        if (!$stmt instanceof PDOStatement) {
            return null;
        }

        $result = $stmt->execute(['id' => $workoutId]);

        if ($result === false) {
            return null;
        }

        /** @var array<string, mixed>|false $workout */
        $workout = $stmt->fetch(PDO::FETCH_ASSOC);

        if ($workout !== false && isset($workout['exercises']) && is_string($workout['exercises'])) {
            /** @var array<int, array<string, mixed>>|null $exercises */
            $exercises = json_decode($workout['exercises'], true);
            $workout['exercises'] = is_array($exercises) ? $exercises : [];
        } elseif ($workout !== false) {
            $workout['exercises'] = [];
        }

        return $workout !== false ? $workout : null;
    }
}
