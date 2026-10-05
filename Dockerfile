# ---------- Steg 1: Bygg ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["JensenOnline.Api/JensenOnline.Api.csproj", "JensenOnline.Api/"]
RUN dotnet restore "JensenOnline.Api/JensenOnline.Api.csproj"

COPY JensenOnline.Api/ JensenOnline.Api/

# Frontenden kopieras in i API:ts wwwroot och serveras från samma adress
COPY frontend/ JensenOnline.Api/wwwroot/

RUN dotnet publish "JensenOnline.Api/JensenOnline.Api.csproj" -c Release -o /app/publish

# ---------- Steg 2: Kör ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Mapp för databasen, ägd av app-användaren
RUN mkdir -p /app/data && chown $APP_UID /app/data

# Kör som vanlig användare, inte root (least privilege)
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "JensenOnline.Api.dll"]