FROM composer:2.6 AS composer

FROM php:8.2-apache

# Установка зависимостей системы
RUN apt-get update && apt-get install -y \
    libpq-dev \
    git \
    unzip \
    && docker-php-ext-install pdo pdo_pgsql bcmath sockets

# Включаем mod_rewrite
RUN a2enmod rewrite

# Копируем composer из первого этапа
COPY --from=composer /usr/bin/composer /usr/bin/composer

# Копируем код
COPY . /var/www/html/

WORKDIR /var/www/html

# Устанавливаем зависимости если есть composer.json
RUN if [ -f "composer.json" ]; then composer install --no-dev --optimize-autoloader --prefer-dist; fi

# Настраиваем Apache
RUN sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/sites-available/000-default.conf \
    && sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/apache2.conf \
    && sed -i 's|AllowOverride None|AllowOverride All|g' /etc/apache2/apache2.conf \
    && chown -R www-data:www-data /var/www/html

EXPOSE 80
