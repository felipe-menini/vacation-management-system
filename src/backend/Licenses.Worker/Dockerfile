FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/backend/Licenses.Worker/Licenses.Worker.csproj ./src/backend/Licenses.Worker/
COPY src/backend/Licenses.Application/Licenses.Application.csproj ./src/backend/Licenses.Application/
COPY src/backend/Licenses.Domain/Licenses.Domain.csproj ./src/backend/Licenses.Domain/
COPY src/backend/Licenses.Infrastructure/Licenses.Infrastructure.csproj ./src/backend/Licenses.Infrastructure/
RUN dotnet restore ./src/backend/Licenses.Worker/Licenses.Worker.csproj

COPY . .
RUN dotnet publish ./src/backend/Licenses.Worker/Licenses.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Licenses.Worker.dll"]
