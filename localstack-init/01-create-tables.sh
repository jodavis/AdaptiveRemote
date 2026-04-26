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

# Create the Dead Letter Queue for layout processing (must be created before the main queue)
awslocal sqs create-queue \
    --queue-name LayoutProcessingDLQ \
    --attributes MessageRetentionPeriod=1209600 \
    --region us-east-1

echo "SQS DLQ 'LayoutProcessingDLQ' created successfully"

# Get the DLQ ARN
DLQ_ARN=$(awslocal sqs get-queue-attributes \
    --queue-url http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/LayoutProcessingDLQ \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text \
    --region us-east-1)

echo "DLQ ARN: $DLQ_ARN"

# Create the main layout processing queue with redrive policy (max receive count = 3)
awslocal sqs create-queue \
    --queue-name LayoutProcessingQueue \
    --attributes \
        VisibilityTimeout=60 \
        "RedrivePolicy={\"deadLetterTargetArn\":\"$DLQ_ARN\",\"maxReceiveCount\":\"3\"}" \
    --region us-east-1

echo "SQS queue 'LayoutProcessingQueue' created successfully"
