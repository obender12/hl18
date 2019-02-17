FROM microsoft/dotnet:3.0-sdk AS build-env
WORKDIR /app

# Copy sources and build
COPY *.csproj ./
COPY src/ ./src/
COPY Properties/*.* ./Properties/
RUN dotnet publish -c Release -r linux-x64 -o out

# Build runtime image
FROM microsoft/dotnet:3.0-runtime
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "hl18.dll"]
