<?php

namespace Rebuilder\ApiGateway\Common;

class EnsiErrorHandler
{
    /**
     * @param string $message
     * @param string $path
     * @param array<string, mixed> $details
     * @return array{
     *     status: int,
     *     error: string,
     *     message: string,
     *     path: string,
     *     timestamp: string,
     *     details: array<string, mixed>
     * }
     */
    public static function unauthorized(
        string $message,
        string $path,
        array $details = []
    ): array {
        return [
            'status' => 401,
            'error' => 'Unauthorized',
            'message' => $message,
            'path' => $path,
            'timestamp' => date('Y-m-d H:i:s'),
            'details' => $details
        ];
    }

    /**
     * @param string $message
     * @param string $path
     * @param array<string, mixed> $details
     * @return array{
     *     status: int,
     *     error: string,
     *     message: string,
     *     path: string,
     *     timestamp: string,
     *     details: array<string, mixed>
     * }
     */
    public static function serverError(
        string $message,
        string $path,
        array $details = []
    ): array {
        return [
            'status' => 500,
            'error' => 'Internal Server Error',
            'message' => $message,
            'path' => $path,
            'timestamp' => date('Y-m-d H:i:s'),
            'details' => $details
        ];
    }

    /**
     * @param string $message
     * @param string $path
     * @return array{
     *     status: int,
     *     error: string,
     *     message: string,
     *     path: string,
     *     timestamp: string
     * }
     */
    public static function databaseError(string $message, string $path): array
    {
        return [
            'status' => 500,
            'error' => 'Database Error',
            'message' => $message,
            'path' => $path,
            'timestamp' => date('Y-m-d H:i:s')
        ];
    }

    /**
     * @param string $message
     * @param string $path
     * @return array{
     *     status: int,
     *     error: string,
     *     message: string,
     *     path: string,
     *     timestamp: string
     * }
     */
    public static function validationError(string $message, string $path): array
    {
        return [
            'status' => 400,
            'error' => 'Validation Error',
            'message' => $message,
            'path' => $path,
            'timestamp' => date('Y-m-d H:i:s')
        ];
    }

    /**
     * @param string $message
     * @param string $path
     * @return array{
     *     status: int,
     *     error: string,
     *     message: string,
     *     path: string,
     *     timestamp: string
     * }
     */
    public static function notFound(string $message, string $path): array
    {
        return [
            'status' => 404,
            'error' => 'Not Found',
            'message' => $message,
            'path' => $path,
            'timestamp' => date('Y-m-d H:i:s')
        ];
    }
}
