<?php

return (new PhpCsFixer\Config())
    ->setRiskyAllowed(true)  // <-- ДОБАВИТЬ ЭТО
    ->setRules([
        '@PSR12' => true,
        'array_syntax' => ['syntax' => 'short'],
        'ordered_imports' => ['sort_algorithm' => 'alpha'],
        'no_unused_imports' => true,
        'strict_param' => true,
        'declare_strict_types' => true,
    ])
    ->setFinder(
        PhpCsFixer\Finder::create()
            ->in(__DIR__ . '/src')
            ->exclude('vendor')
    );