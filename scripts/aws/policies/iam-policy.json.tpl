{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "S3Upload",
      "Effect": "Allow",
      "Action": ["s3:PutObject", "s3:DeleteObject", "s3:ListBucket"],
      "Resource": [
        "arn:aws:s3:::scratch-link.aluxcoding.com",
        "arn:aws:s3:::scratch-link.aluxcoding.com/*",
        "arn:aws:s3:::dev-scratch-link.aluxcoding.com",
        "arn:aws:s3:::dev-scratch-link.aluxcoding.com/*"
      ]
    },
    {
      "Sid": "CloudFrontInvalidate",
      "Effect": "Allow",
      "Action": ["cloudfront:CreateInvalidation", "cloudfront:GetInvalidation"],
      "Resource": [
        "arn:aws:cloudfront::__ACCOUNT__:distribution/__PROD_DIST_ID__",
        "arn:aws:cloudfront::__ACCOUNT__:distribution/__DEV_DIST_ID__"
      ]
    }
  ]
}
