# Task #2 Evidence — Lambda S3 Status

Generated verification notes for the Word report screenshots section.
Capture the listed AWS Console pages manually; CLI results below are saved as proof.

## Console pages to screenshot

Open these in `ap-southeast-1` while signed in with the `ddac` profile account:

1. **Lambda function overview**  
   https://ap-southeast-1.console.aws.amazon.com/lambda/home?region=ap-southeast-1#/functions/zammaintain-s3-status-lambda?tab=code

2. **Configuration → Environment variables** (`BUCKET_NAME=zammaintain-s3`)  
   https://ap-southeast-1.console.aws.amazon.com/lambda/home?region=ap-southeast-1#/functions/zammaintain-s3-status-lambda?tab=configure

3. **Configuration → Permissions** (role `zammaintain-s3-status-lambda-role`)  
   Same Configure tab → Permissions  
   Role URL:  
   https://us-east-1.console.aws.amazon.com/iam/home#/roles/details/zammaintain-s3-status-lambda-role

4. **Test → TestS3Status** (event `{}`, result status 200)  
   https://ap-southeast-1.console.aws.amazon.com/lambda/home?region=ap-southeast-1#/functions/zammaintain-s3-status-lambda?tab=testing

5. **API Gateway GET /s3-status** (browser JSON)  
   https://9gumsgcdi7.execute-api.ap-southeast-1.amazonaws.com/prod/s3-status

6. **CloudWatch dashboard**  
   https://ap-southeast-1.console.aws.amazon.com/cloudwatch/home?region=ap-southeast-1#dashboards:name=zammaintain-task2-monitoring

7. **CloudWatch log group**  
   https://ap-southeast-1.console.aws.amazon.com/cloudwatch/home?region=ap-southeast-1#logsV2:log-groups/log-group/$252Faws$252Flambda$252Fzammaintain-s3-status-lambda

## CLI verification captured

### Lambda configuration (verified)

- Name: `zammaintain-s3-status-lambda`
- Runtime: `python3.12`
- Handler: `lambda_function.lambda_handler`
- State: `Active`
- Env: `BUCKET_NAME=zammaintain-s3`
- Role: `arn:aws:iam::713054633555:role/zammaintain-s3-status-lambda-role`
- Policies: `AWSLambdaBasicExecutionRole`, `AmazonS3ReadOnlyAccess`

### TestS3Status invoke (verified)

- Invoke HTTP StatusCode: **200**
- Response statusCode: **200**
- Bucket: `zammaintain-s3`
- Folders: `avatars/`, `issues/`, `proofs/`, `properties/`, `site/`, `technician-documents/`
- Object count: **16**
- Evidence files:
  - `TestS3Status-response.json`
  - `TestS3Status-response-pretty.json`

### API Gateway GET /s3-status

Evidence file (created when tested):

- `api-s3-status-response.json`
