# ZamMaintain S3 Status Lambda

AWS Lambda function that lists folders and objects in the private `zammaintain-s3` bucket and returns JSON status for Task #2 monitoring demos.

## AWS details

| Setting | Value |
|---|---|
| Function name | `zammaintain-s3-status-lambda` |
| Runtime | Python 3.12 |
| Handler | `lambda_function.lambda_handler` |
| Environment | `BUCKET_NAME=zammaintain-s3` |
| Region | `ap-southeast-1` |
| IAM role | `zammaintain-s3-status-lambda-role` |
| Policies | `AWSLambdaBasicExecutionRole`, `AmazonS3ReadOnlyAccess` |

## Redeploy code

```powershell
$env:AWS_PROFILE = "ddac"
$env:AWS_REGION = "ap-southeast-1"

$zip = Join-Path $env:TEMP "zammaintain-s3-status-lambda.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path .\lambda_function.py -DestinationPath $zip -Force

aws lambda update-function-code `
  --function-name zammaintain-s3-status-lambda `
  --zip-file "fileb://$zip" `
  --profile ddac `
  --region ap-southeast-1
```

## Test event (TestS3Status)

Payload:

```json
{}
```

Invoke:

```powershell
aws lambda invoke `
  --function-name zammaintain-s3-status-lambda `
  --payload "{}" `
  --cli-binary-format raw-in-base64-out `
  --profile ddac `
  --region ap-southeast-1 `
  response.json

Get-Content response.json
```
