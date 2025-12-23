<?php

declare(strict_types=1);

namespace Rebuilder\TextToSpeech\Database;

use PDO;
use PDOException;

class TTSDatabaseConnection
{
    /** @var self|null */
    private static ?self $instance = null;

    /** @var PDO */
    private PDO $connection;

    private function __construct()
    {
        // Функция для безопасного получения строки из env
        $getString = function (string $key, string $default): string {
            $value = $_ENV[$key] ?? $default;

            // Используем filter_var для безопасного получения строки
            $result = filter_var($value, FILTER_UNSAFE_RAW, FILTER_REQUIRE_SCALAR | FILTER_FLAG_STRIP_LOW);

            return $result !== false ? $result : $default;
        };

        $host = $getString('DB_HOST', 'tts-db');
        $dbname = $getString('DB_NAME', 'tts_db');
        $username = $getString('DB_USER', 'postgres');
        $password = $getString('DB_PASS', 'password');

        $dsn = "pgsql:host={$host};port=5432;dbname={$dbname}";

        try {
            $this->connection = new PDO($dsn, $username, $password, [
                PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC
            ]);
        } catch (PDOException $e) {
            throw new PDOException("TTS Database connection failed: " . $e->getMessage());
        }
    }

    public static function getInstance(): self
    {
        if (self::$instance === null) {
            self::$instance = new self();
        }
        return self::$instance;
    }

    public function getConnection(): PDO
    {
        return $this->connection;
    }
}
