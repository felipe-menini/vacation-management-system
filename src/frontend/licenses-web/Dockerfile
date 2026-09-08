FROM node:24-alpine AS build
WORKDIR /app

COPY src/frontend/licenses-web/package*.json ./
RUN npm ci

COPY src/frontend/licenses-web/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY src/frontend/licenses-web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
