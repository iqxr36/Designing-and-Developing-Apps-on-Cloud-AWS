import json
import os
import boto3

s3 = boto3.client("s3")


def lambda_handler(event, context):
    bucket = os.environ.get("BUCKET_NAME")
    if not bucket:
        return {
            "statusCode": 500,
            "headers": {"Content-Type": "application/json"},
            "body": json.dumps({"error": "BUCKET_NAME is not configured"}),
        }

    # List top-level "folders" (common prefixes)
    prefixes_resp = s3.list_objects_v2(Bucket=bucket, Delimiter="/")
    folders = [p.get("Prefix") for p in prefixes_resp.get("CommonPrefixes", [])]

    # List all objects (exclude folder placeholder keys that end with / and size 0)
    objects = []
    paginator = s3.get_paginator("list_objects_v2")
    for page in paginator.paginate(Bucket=bucket):
        for obj in page.get("Contents", []):
            key = obj["Key"]
            size = obj["Size"]
            if key.endswith("/") and size == 0:
                continue
            objects.append(
                {
                    "key": key,
                    "size": size,
                    "lastModified": obj["LastModified"].isoformat(),
                }
            )

    payload = {
        "statusCode": 200,
        "bucket": bucket,
        "folders": folders,
        "objectCount": len(objects),
        "objects": objects,
        "message": "S3 status retrieved successfully",
    }

    return {
        "statusCode": 200,
        "headers": {"Content-Type": "application/json"},
        "body": json.dumps(payload),
    }
