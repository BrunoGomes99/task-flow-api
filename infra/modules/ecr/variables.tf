variable "repository_name" {
  description = "Name of the ECR repository for the TaskFlow API image."
  type        = string
  default     = "taskflow-api"
}

variable "untagged_image_expiration_days" {
  description = "Expire untagged images after this many days."
  type        = number
  default     = 14
}
