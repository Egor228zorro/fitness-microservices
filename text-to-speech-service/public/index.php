<?php
declare(strict_types=1);

// Включаем вывод ошибок для разработки
error_reporting(E_ALL);
ini_set('display_errors', '1');

// Загружаем автозагрузчик
require_once '/var/www/html/vendor/autoload.php';

// Создаем приложение text-to-speech-service
$app = new \Rebuilder\TextToSpeech\Application();

// Запускаем приложение
$app->run();