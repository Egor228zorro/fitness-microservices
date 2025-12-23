FROM php:8.1-apache 
 
# Устанавливаем системные зависимости + Supervisor 
RUN apt-get update && apt-get install -y \ 
 libpq-dev \ 
 git \ 
 unzip \ 
 supervisor \ 
 && docker-php-ext-install pdo pdo_pgsql bcmath sockets 
 
RUN a2enmod rewrite 
 
# Копируем конфиг Supervisor 
COPY supervisor/ /etc/supervisor/conf.d/ 
 
# Копируем исходный код 
COPY . /var/www/html/ 
 
# Используем готовый Composer из multi-stage build 
COPY --from=composer:2.6 /usr/bin/composer /usr/bin/composer 
 
# ФИКСИМ ПУТЬ В TTSWorker.php ПЕРЕД установкой зависимостей 
RUN sed -i "s|require_once __DIR__ . '/../vendor/autoload.php';|require_once '/var/www/html/vendor/autoload.php';|g" /var/www/html/src/Worker/TTSWorker.php 
 
WORKDIR /var/www/html 
 
# УВЕЛИЧИВАЕМ ТАЙМАУТ КОМПОЗЕРА (ДОБАВЛЯЕМ ЭТО!) 
ENV COMPOSER_PROCESS_TIMEOUT=600 \ 
 COMPOSER_INSTALL_TIMEOUT=600 
 
# Устанавливаем зависимости Composer 
RUN composer install --no-dev --optimize-autoloader --no-interaction --prefer-dist 
 
# Настройка Apache 
RUN sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/sites-available/000-default.conf 
RUN sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/apache2.conf 
 
RUN chown -R www-data:www-data /var/www/html 
 
# Запускаем Supervisor 
CMD ["/usr/bin/supervisord", "-n", "-c", "/etc/supervisor/supervisord.conf"] 
 
EXPOSE 80