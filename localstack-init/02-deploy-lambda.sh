#!/bin/bash

# Deploy LayoutCompilerService as a Lambda function to LocalStack.
# The package must be pre-built before running this script:
#   dotnet publish src/AdaptiveRemote.Backend.LayoutCompilerService -c Release -r linux-x64
#   zip -j /tmp/bootstrap.zip src/AdaptiveRemote.Backend.LayoutCompilerService/bin/Release/net10.0/linux-x64/publish/bootstrap

FUNCTION_NAME="LayoutCompilerService"
REGION="us-east-1"
PACKAGE_PATH="${LAMBDA_PACKAGE_PATH:-/tmp/bootstrap.zip}"

if [ ! -f "$PACKAGE_PATH" ]; then
    echo "Lambda package not found at $PACKAGE_PATH. Skipping Lambda deployment."
    echo "To deploy, build the package first and set LAMBDA_PACKAGE_PATH."
    exit 0
fi

# Create or update the function
awslocal lambda create-function \
    --function-name "$FUNCTION_NAME" \
    --runtime provided.al2023 \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --handler bootstrap \
    --zip-file "fileb://$PACKAGE_PATH" \
    --region "$REGION" \
    --environment Variables="{ASPNETCORE_ENVIRONMENT=Development}" 2>/dev/null \
|| awslocal lambda update-function-code \
    --function-name "$FUNCTION_NAME" \
    --zip-file "fileb://$PACKAGE_PATH" \
    --region "$REGION"

echo "Lambda function '$FUNCTION_NAME' deployed successfully"

# Expose via Function URL for HTTP access
awslocal lambda create-function-url-config \
    --function-name "$FUNCTION_NAME" \
    --auth-type NONE \
    --region "$REGION" 2>/dev/null || true

echo "Lambda function URL created for '$FUNCTION_NAME'"
