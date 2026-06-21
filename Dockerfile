FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project file
COPY *.csproj ./
RUN dotnet restore

# Copy source code
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install System.Drawing dependencies
RUN apt-get update && apt-get install -y \
    libfreetype6 \
    libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out .

EXPOSE 5001

ENV ASPNETCORE_URLS=https://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Диплом.dll"]
