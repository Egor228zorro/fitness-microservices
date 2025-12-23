<?php
require_once __DIR__ . '/../vendor/autoload.php';

use Rebuilder\ApiGateway\Application;

$app = new Application();
$app->run();
