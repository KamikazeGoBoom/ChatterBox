FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY ./bin/Release/net8.0/publish .
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "ChatterBox.dll"]
