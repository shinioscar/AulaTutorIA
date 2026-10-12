FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /source
COPY global.json Directory.Build.props NuGet.Config ./
COPY src/AulaTutor.Domain/ src/AulaTutor.Domain/
COPY src/AulaTutor.Application/ src/AulaTutor.Application/
COPY src/AulaTutor.Infrastructure/ src/AulaTutor.Infrastructure/
COPY src/AulaTutor.Api/ src/AulaTutor.Api/
RUN dotnet restore src/AulaTutor.Api/AulaTutor.Api.csproj --locked-mode
RUN dotnet publish src/AulaTutor.Api/AulaTutor.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AulaTutor.Api.dll"]
