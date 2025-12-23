FROM php:8.1-apache

RUN a2enmod rewrite

# Копируем исходный код
COPY . /var/www/html/

# Меняем DocumentRoot на public
RUN sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/sites-available/000-default.conf
RUN sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/apache2.conf

# Устанавливаем права
RUN chown -R www-data:www-data /var/www/html

WORKDIR /var/www/html
EXPOSE 80