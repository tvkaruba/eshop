# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ES.Client/ES.Client.csproj", "ES.Client/"]
RUN dotnet restore "ES.Client/ES.Client.csproj"
COPY . .
WORKDIR "/src/ES.Client"
RUN dotnet build "ES.Client.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ES.Client.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Serve stage
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=publish /app/publish/wwwroot .
COPY ES.Client/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80 