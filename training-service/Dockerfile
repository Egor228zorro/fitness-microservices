FROM php:8.1-apache

# УСТАНАВЛИВАЕМ системные зависимости и PHP расширения
RUN apt-get update && apt-get install -y \
    libpq-dev \
    git \
    unzip \
    && docker-php-ext-install pdo pdo_pgsql bcmath sockets

RUN a2enmod rewrite

# УСТАНАВЛИВАЕМ Composer
COPY --from=composer:latest /usr/bin/composer /usr/bin/composer

# Копируем исходный код
COPY . /var/www/html/

WORKDIR /var/www/html

# Копируем ТОЛЬКО composer.json (composer.lock может отсутствовать)
COPY composer.json ./

# УСТАНАВЛИВАЕМ зависимости через Composer с теми же флагами
RUN composer install --no-dev --optimize-autoloader --ignore-platform-req=ext-sockets --ignore-platform-req=php --prefer-dist

# Меняем DocumentRoot на public
RUN sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/sites-available/000-default.conf
RUN sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/apache2.conf

# ✅ ДОБАВЛЕНО: Разрешаем .htaccess
RUN sed -i 's|AllowOverride None|AllowOverride All|g' /etc/apache2/apache2.conf

# Устанавливаем права
RUN chown -R www-data:www-data /var/www/html

EXPOSE 80
