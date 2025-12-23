<?php
declare(strict_types=1);

require_once __DIR__ . '/../vendor/autoload.php';

use Rebuilder\Training\Application;

// Создаем и запускаем приложение
$app = new Application();
$app->run();