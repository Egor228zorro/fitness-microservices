<?php

namespace Rebuilder\Training\Common;

/**
 * Обработчик ошибок в формате ENSE
 * @link https://docs.ensi.tech/guidelines/api#errors
 */
class EnsiErrorHandler
{
    /**
     * @param array<string, mixed> $details
     * @return array<string, mixed>
     */
    public static function validationError(string $message, string $path, array $details = []): array
    {
        return [
            'type' => 'validation_error',
            'message' => $message,
            'path' => $path,
            'status' => 400,
            'timestamp' => date('c'),
            'details' => $details,
            'links' => [
                'about' => 'https://docs.ensi.tech/guidelines/api#errors'
            ]
        ];
    }

    /**
     * @param array<string, mixed> $details
     * @return array<string, mixed>
     */
    public static function databaseError(string $message, string $path, array $details = []): array
    {
        return [
            'type' => 'database_error',
            'message' => $message,
            'path' => $path,
            'status' => 500,
            'timestamp' => date('c'),
            'details' => $details,
            'links' => [
                'about' => 'https://docs.ensi.tech/guidelines/api#errors'
            ]
        ];
    }

    /**
     * @param array<string, mixed> $details
     * @return array<string, mixed>
     */
    public static function notFound(string $message, string $path, array $details = []): array
    {
        return [
            'type' => 'not_found',
            'message' => $message,
            'path' => $path,
            'status' => 404,
            'timestamp' => date('c'),
            'details' => $details,
            'links' => [
                'about' => 'https://docs.ensi.tech/guidelines/api#errors'
            ]
        ];
    }

    /**
     * @param array<string, mixed> $details
     * @return array<string, mixed>
     */
    public static function serverError(string $message, string $path, array $details = []): array
    {
        return [
            'type' => 'server_error',
            'message' => $message,
            'path' => $path,
            'status' => 500,
            'timestamp' => date('c'),
            'details' => $details,
            'links' => [
                'about' => 'https://docs.ensi.tech/guidelines/api#errors'
            ]
        ];
    }

    /**
     * @param array<string, mixed> $details
     * @return array<string, mixed>
     */
    public static function notFoundError(string $message, string $path, array $details = []): array
    {
        return [
            'type' => 'not_found',
            'message' => $message,
            'path' => $path,
            'status' => 404,
            'timestamp' => date('c'),
            'details' => $details,
            'links' => [
                'about' => 'https://docs.ensi.tech/guidelines/api#errors'
            ]
        ];
    }

    /**
     * @param array<string, mixed> $details
     * @return array<string, mixed>
     */
    public static function externalServiceError(string $message, string $path, array $details = []): array
    {
        return [
            'type' => 'external_service_error',
            'message' => $message,
            'path' => $path,
            'status' => 502,
            'timestamp' => date('c'),
            'details' => $details,
            'links' => [
                'about' => 'https://docs.ensi.tech/guidelines/api#errors'
            ]
        ];
    }

    /**
     * @param array<string, mixed> $details
     * @return array<string, mixed>
     */
    public static function unauthorized(string $message, string $path, array $details = []): array
    {
        return [
            'type' => 'unauthorized',
            'message' => $message,
            'path' => $path,
            'status' => 403,
            'timestamp' => date('c'),
            'details' => $details,
            'links' => [
                'about' => 'https://docs.ensi.tech/guidelines/api#errors'
            ]
        ];
    }
}
