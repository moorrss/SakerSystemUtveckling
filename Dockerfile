FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src


COPY ["JensenOnline.Api/JensenOnline.Api.csproj", "JensenOnline.Api/"]
RUN dotnet restore "JensenOnline.Api/JensenOnline.Api.csproj"


COPY JensenOnline.Api/ JensenOnline.Api/
RUN dotnet publish "JensenOnline.Api/JensenOnline.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .


USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "JensenOnline.Api.dll"]