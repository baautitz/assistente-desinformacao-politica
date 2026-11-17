FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AssistenteDesinformacao.Bot -c Release -o /app/bot \
 && dotnet publish src/AssistenteDesinformacao.Coletor -c Release -o /app/coletor

# Uma imagem só, com os dois processos. O padrão é o bot; o serviço coletor troca o entrypoint no compose.
FROM mcr.microsoft.com/dotnet/runtime:10.0
COPY --from=build /app /app
WORKDIR /app/bot
ENTRYPOINT ["dotnet", "AssistenteDesinformacao.Bot.dll"]
