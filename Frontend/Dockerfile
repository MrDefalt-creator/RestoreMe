FROM node:22-alpine AS build
WORKDIR /app

COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile

COPY . .
RUN yarn build

FROM httpd:2.4.66 AS final
LABEL authors="mrdefalt"

COPY --from=build /app/dist/ /usr/local/apache2/htdocs/
