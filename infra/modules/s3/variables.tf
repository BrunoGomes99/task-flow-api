variable "name_prefix" {
  description = "Prefix applied to the email asset bucket name."
  type        = string
}

variable "logo_path" {
  description = "Path of the PNG uploaded as email/logo.png."
  type        = string

  validation {
    condition     = fileexists(var.logo_path) && endswith(lower(var.logo_path), ".png")
    error_message = "logo_path must be an existing PNG file."
  }
}
