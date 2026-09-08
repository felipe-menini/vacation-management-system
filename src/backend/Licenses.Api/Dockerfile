FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/backend/Licenses.slnx ./src/backend/
COPY src/backend/Licenses.Api/Licenses.Api.csproj ./src/backend/Licenses.Api/
COPY src/backend/Licenses.Application/Licenses.Application.csproj ./src/backend/Licenses.Application/
COPY src/backend/Licenses.Domain/Licenses.Domain.csproj ./src/backend/Licenses.Domain/
COPY src/backend/Licenses.Infrastructure/Licenses.Infrastructure.csproj ./src/backend/Licenses.Infrastructure/
COPY src/backend/Licenses.Worker/Licenses.Worker.csproj ./src/backend/Licenses.Worker/
COPY tests/backend/Licenses.Api.Tests/Licenses.Api.Tests.csproj ./tests/backend/Licenses.Api.Tests/
COPY tests/backend/Licenses.Application.Tests/Licenses.Application.Tests.csproj ./tests/backend/Licenses.Application.Tests/
COPY tests/backend/Licenses.Domain.Tests/Licenses.Domain.Tests.csproj ./tests/backend/Licenses.Domain.Tests/
RUN dotnet restore ./src/backend/Licenses.Api/Licenses.Api.csproj

COPY . .
RUN dotnet publish ./src/backend/Licenses.Api/Licenses.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Licenses.Api.dll"]
