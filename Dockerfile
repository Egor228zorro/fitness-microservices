FROM php:8.1-apache

RUN apt-get update && apt-get install -y \
    libpq-dev \
    supervisor \
    git \
    unzip \
    && docker-php-ext-install pdo pdo_pgsql sockets \
    && a2enmod rewrite \
    && apt-get clean

COPY . /var/www/html/

WORKDIR /var/www/html

RUN curl -sS https://getcomposer.org/installer | php -- --install-dir=/usr/local/bin --filename=composer
RUN composer install --no-dev --optimize-autoloader --ignore-platform-reqs --no-interaction --prefer-dist

RUN echo 'ServerName localhost' >> /etc/apache2/apache2.conf
RUN sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/sites-available/000-default.conf

# СОЗДАЕМ ДИРЕКТОРИИ И ДАЕМ ПРАВА
RUN mkdir -p /var/run/apache2 /var/lock/apache2 /var/log/apache2
RUN chown -R www-data:www-data /var/run/apache2 /var/lock/apache2 /var/log/apache2
RUN chmod -R 755 /var/log/apache2
RUN touch /var/log/apache2/error.log /var/log/apache2/access.log
RUN chown www-data:www-data /var/log/apache2/*.log

RUN chown -R www-data:www-data /var/www/html

COPY supervisor/supervisord.conf /etc/supervisor/conf.d/

CMD ["/usr/bin/supervisord", "-n", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
