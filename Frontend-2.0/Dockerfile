FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_API_BASE_URL=http://localhost:8080
ARG VITE_API_MODE=live
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_API_MODE=$VITE_API_MODE

COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile

COPY . .
RUN yarn build

FROM httpd:2.4.66 AS final
RUN sed -i 's/^#LoadModule rewrite_module modules\/mod_rewrite.so/LoadModule rewrite_module modules\/mod_rewrite.so/' /usr/local/apache2/conf/httpd.conf \
    && sed -i 's/^#LoadModule headers_module modules\/mod_headers.so/LoadModule headers_module modules\/mod_headers.so/' /usr/local/apache2/conf/httpd.conf \
    && printf "ServerName localhost\n" > /usr/local/apache2/conf/extra/servername.conf \
    && printf "<Directory \"/usr/local/apache2/htdocs\">\n    Options Indexes FollowSymLinks\n    AllowOverride None\n    Require all granted\n    RewriteEngine On\n    RewriteCond %%{REQUEST_FILENAME} -f [OR]\n    RewriteCond %%{REQUEST_FILENAME} -d\n    RewriteRule ^ - [L]\n    RewriteRule ^ /index.html [L]\n</Directory>\n" > /usr/local/apache2/conf/extra/spa-routing.conf \
    && printf "<LocationMatch \"^/assets/.*\\.(js|css|svg|png|jpg|jpeg|gif|webp|ico|woff2?)$\">\n    Header set Cache-Control \"public, max-age=31536000, immutable\"\n</LocationMatch>\n<LocationMatch \"^/(index\\.html)?$\">\n    Header set Cache-Control \"no-cache, no-store, must-revalidate\"\n</LocationMatch>\n" > /usr/local/apache2/conf/extra/cache-control.conf \
    && echo "Include conf/extra/servername.conf" >> /usr/local/apache2/conf/httpd.conf \
    && echo "Include conf/extra/spa-routing.conf" >> /usr/local/apache2/conf/httpd.conf \
    && echo "Include conf/extra/cache-control.conf" >> /usr/local/apache2/conf/httpd.conf

COPY --from=build /app/dist/ /usr/local/apache2/htdocs/
