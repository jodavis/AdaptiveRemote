#!/bin/bash

# Create DynamoDB table for RawLayouts
awslocal dynamodb create-table \
    --table-name RawLayouts \
    --attribute-definitions \
        AttributeName=UserId,AttributeType=S \
        AttributeName=Id,AttributeType=S \
    --key-schema \
        AttributeName=UserId,KeyType=HASH \
        AttributeName=Id,KeyType=RANGE \
    --billing-mode PAY_PER_REQUEST \
    --region us-east-1

echo "DynamoDB table 'RawLayouts' created successfully"
