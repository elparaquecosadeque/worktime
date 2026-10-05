# Builds both locales (/es/, /en/) and serves them from nginx, which also balances the two API replicas.
FROM node:24-alpine AS build
WORKDIR /web
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/web/browser /usr/share/nginx/html
EXPOSE 80
