@echo off
echo Azure App Service Deployment Script (Simplified)
echo ================================================

:: Force Node.js version for Azure App Service
echo Setting Node.js version to 18.17.0...
set WEBSITE_NODE_DEFAULT_VERSION=18.17.0
set NODE_VERSION=18.17.0

:: Force specific Node.js paths for Azure App Service
echo Checking Node.js version...
node --version
echo Checking npm version...
npm --version
set "NPM_CMD=npm"

:: Build Frontend with clean install
echo Building React Frontend...
cd Frontend
echo Cleaning node_modules...
if exist node_modules rmdir /s /q node_modules
echo Installing packages with clean cache...
echo Testing npm command: %NPM_CMD%
%NPM_CMD% --version
if %ERRORLEVEL% neq 0 (
    echo NPM command failed, trying direct npm
    set "NPM_CMD=npm"
)
call %NPM_CMD% cache clean --force
call %NPM_CMD% install --legacy-peer-deps --no-optional --no-audit --no-fund --progress=false --loglevel=error
call %NPM_CMD% run build
if %ERRORLEVEL% neq 0 goto error
cd ..

:: Build Backend
echo Building .NET Backend...
cd Backend
call dotnet clean
call dotnet restore --no-cache
if %ERRORLEVEL% neq 0 goto error
call dotnet publish --configuration Release --output %DEPLOYMENT_TARGET% --no-restore
if %ERRORLEVEL% neq 0 goto error
cd ..

:: Copy Frontend to wwwroot
echo Copying Frontend to wwwroot...
if exist "Frontend\dist" (
    if not exist "%DEPLOYMENT_TARGET%\wwwroot" mkdir "%DEPLOYMENT_TARGET%\wwwroot"
    xcopy /E /I /Y "Frontend\dist\*" "%DEPLOYMENT_TARGET%\wwwroot\"
    echo Frontend copied successfully
) else (
    echo ERROR: Frontend build not found at Frontend\dist
    goto error
)

:: Don't copy Frontend web.config - Backend web.config handles both API and SPA routing
echo Skipping Frontend web.config copy - using consolidated Backend web.config

echo Deployment completed successfully!
goto end

:error
echo Deployment failed with error code %ERRORLEVEL%
echo Current directory: %CD%
echo DEPLOYMENT_TARGET: %DEPLOYMENT_TARGET%
exit /b %ERRORLEVEL%

:end
echo Done.
