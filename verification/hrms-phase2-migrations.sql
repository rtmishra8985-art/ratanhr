CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `analytics_snapshots` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `snapshot_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `period` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `value` numeric(18,4) NOT NULL,
    `metadata` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_analytics_snapshots` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `appreciations` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NULL,
    `award_title` varchar(200) CHARACTER SET utf8mb4 NULL,
    `description` longtext CHARACTER SET utf8mb4 NULL,
    `message` longtext CHARACTER SET utf8mb4 NULL,
    `file_path` longtext CHARACTER SET utf8mb4 NULL,
    `certificate_path` longtext CHARACTER SET utf8mb4 NULL,
    `awarded_by_user_id` int NULL,
    `created_by` int NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NULL,
    `deleted_at` datetime(6) NULL,
    CONSTRAINT `PK_appreciations` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `asset_categories` (
    `id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `company_id` int NOT NULL,
    CONSTRAINT `PK_asset_categories` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `attendance_devices` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `employee_id` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `device_fingerprint` varchar(512) CHARACTER SET utf8mb4 NOT NULL,
    `device_type` varchar(50) CHARACTER SET utf8mb4 NULL,
    `browser` varchar(200) CHARACTER SET utf8mb4 NULL,
    `last_ip_address` varchar(50) CHARACTER SET utf8mb4 NULL,
    `is_trusted` tinyint(1) NOT NULL DEFAULT TRUE,
    `first_seen_at` datetime(6) NOT NULL,
    `last_seen_at` datetime(6) NOT NULL,
    `use_count` int NOT NULL DEFAULT 1,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NULL,
    CONSTRAINT `PK_attendance_devices` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `attendance_location_audit` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `employee_id` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `latitude` double NOT NULL,
    `longitude` double NOT NULL,
    `accuracy` double NULL,
    `geofence_id` int NULL,
    `distance_metres` double NULL,
    `is_inside_geofence` tinyint(1) NOT NULL,
    `was_allowed` tinyint(1) NOT NULL,
    `event_type` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `ip_address` varchar(50) CHARACTER SET utf8mb4 NULL,
    `browser` varchar(200) CHARACTER SET utf8mb4 NULL,
    `device_type` varchar(50) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_attendance_location_audit` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `audit_logs` (
    `id` bigint NOT NULL AUTO_INCREMENT,
    `action` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `entity_type` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `entity_id` varchar(100) CHARACTER SET utf8mb4 NULL,
    `performed_by` int NULL,
    `performed_by_name` varchar(200) CHARACTER SET utf8mb4 NULL,
    `ip_address` varchar(45) CHARACTER SET utf8mb4 NULL,
    `details` longtext CHARACTER SET utf8mb4 NULL,
    `success` tinyint(1) NOT NULL DEFAULT TRUE,
    `occurred_at` datetime(6) NOT NULL,
    `company_id` int NULL,
    CONSTRAINT `PK_audit_logs` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `biometric_devices` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `provider_type` int NOT NULL,
    `vendor` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `ip_address` varchar(45) CHARACTER SET utf8mb4 NOT NULL,
    `port` int NOT NULL,
    `serial_number` varchar(100) CHARACTER SET utf8mb4 NULL,
    `location` varchar(200) CHARACTER SET utf8mb4 NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `is_enabled` tinyint(1) NOT NULL,
    `last_sync_at` datetime(6) NULL,
    `last_ping_at` datetime(6) NULL,
    `last_error` longtext CHARACTER SET utf8mb4 NULL,
    `firmware_version` longtext CHARACTER SET utf8mb4 NULL,
    `enrolled_user_count` int NULL,
    `connection_params` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_biometric_devices` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `biometric_settings` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `auto_sync_enabled` tinyint(1) NOT NULL,
    `sync_interval_minutes` int NOT NULL,
    `sync_lookback_days` int NOT NULL,
    `grace_time_minutes` int NOT NULL,
    `min_half_day_hours` int NOT NULL,
    `deduplicate_punches` tinyint(1) NOT NULL,
    `dedupe_window_minutes` int NOT NULL,
    `queue_unknown_employees` tinyint(1) NOT NULL,
    `realtime_enabled` tinyint(1) NOT NULL,
    `persist_raw_logs` tinyint(1) NOT NULL,
    `log_retention_days` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_biometric_settings` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `bonuses` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NULL,
    `bonus_type` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `amount` decimal(14,2) NOT NULL,
    `month` int NOT NULL,
    `year` int NOT NULL,
    `remarks` longtext CHARACTER SET utf8mb4 NULL,
    `is_taxable` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_bonuses` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `candidates` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `job_requisition_id` int NULL,
    `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `email` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `phone` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `address` longtext CHARACTER SET utf8mb4 NOT NULL,
    `current_designation` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `current_company` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `total_experience` numeric(4,1) NOT NULL,
    `skills` longtext CHARACTER SET utf8mb4 NOT NULL,
    `qualification_summary` longtext CHARACTER SET utf8mb4 NOT NULL,
    `resume_file_path` varchar(500) CHARACTER SET utf8mb4 NULL,
    `source_channel` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `notes` longtext CHARACTER SET utf8mb4 NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_candidates` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `companies` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `company_founder_name` longtext CHARACTER SET utf8mb4 NULL,
    `phone_number` longtext CHARACTER SET utf8mb4 NULL,
    `email_address` longtext CHARACTER SET utf8mb4 NULL,
    `industry_type` longtext CHARACTER SET utf8mb4 NULL,
    `business_type` longtext CHARACTER SET utf8mb4 NULL,
    `cin` longtext CHARACTER SET utf8mb4 NULL,
    `tin` longtext CHARACTER SET utf8mb4 NULL,
    `pan` longtext CHARACTER SET utf8mb4 NULL,
    `tan` longtext CHARACTER SET utf8mb4 NULL,
    `address_line1` longtext CHARACTER SET utf8mb4 NULL,
    `address_line2` longtext CHARACTER SET utf8mb4 NULL,
    `city` longtext CHARACTER SET utf8mb4 NULL,
    `state_province` longtext CHARACTER SET utf8mb4 NULL,
    `country` longtext CHARACTER SET utf8mb4 NULL DEFAULT ('India'),
    `postal_code` longtext CHARACTER SET utf8mb4 NULL,
    `logo_path` longtext CHARACTER SET utf8mb4 NULL,
    `max_employees` int NULL,
    `is_active` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_companies` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `continuous_feedback` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `from_employee_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `to_employee_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `feedback_text` longtext CHARACTER SET utf8mb4 NOT NULL,
    `feedback_type` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `is_anonymous` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_continuous_feedback` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `deductions` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NULL,
    `deduction_type` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `amount` decimal(14,2) NOT NULL,
    `month` int NOT NULL,
    `year` int NOT NULL,
    `remarks` longtext CHARACTER SET utf8mb4 NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_deductions` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `departments` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_departments` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `designations` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_designations` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `email_queue` (
    `id` int NOT NULL AUTO_INCREMENT,
    `to_address` varchar(320) CHARACTER SET utf8mb4 NOT NULL,
    `subject` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `html_body` longtext CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `retry_count` int NOT NULL DEFAULT 0,
    `last_error` longtext CHARACTER SET utf8mb4 NULL,
    `sent_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `next_retry_at` datetime(6) NULL,
    CONSTRAINT `PK_email_queue` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `employee_documents` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NULL,
    `document_type` longtext CHARACTER SET utf8mb4 NOT NULL,
    `file_name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `file_path` longtext CHARACTER SET utf8mb4 NOT NULL,
    `file_size_bytes` bigint NOT NULL,
    `notes` longtext CHARACTER SET utf8mb4 NULL,
    `is_verified` tinyint(1) NOT NULL DEFAULT FALSE,
    `verified_at` datetime(6) NULL,
    `verified_by_user_id` int NULL,
    `uploaded_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_employee_documents` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `employee_exits` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `exit_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `notice_period_days` int NULL,
    `resignation_date` date NULL,
    `last_working_date` date NULL,
    `reason` longtext CHARACTER SET utf8mb4 NULL,
    `exit_reason` longtext CHARACTER SET utf8mb4 NULL,
    `interview_notes` longtext CHARACTER SET utf8mb4 NULL,
    `is_notice_period_served` tinyint(1) NOT NULL,
    `is_completed` tinyint(1) NOT NULL DEFAULT FALSE,
    `gratuity_amount` decimal(65,30) NULL,
    `settlement_amount` decimal(65,30) NULL,
    `status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `initiated_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `completed_at` datetime(6) NULL,
    CONSTRAINT `PK_employee_exits` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `employee_goals` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `employee_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `performance_cycle_id` int NULL,
    `title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `goal_type` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `category` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `target_value` numeric(18,4) NOT NULL,
    `achieved_value` numeric(18,4) NULL,
    `unit` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `due_date` datetime(6) NOT NULL,
    `status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `weight` int NOT NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_employee_goals` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `employee_promotions` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `from_designation` longtext CHARACTER SET utf8mb4 NULL,
    `to_designation` longtext CHARACTER SET utf8mb4 NULL,
    `from_department` longtext CHARACTER SET utf8mb4 NULL,
    `to_department` longtext CHARACTER SET utf8mb4 NULL,
    `salary_increment` decimal(65,30) NULL,
    `effective_date` date NOT NULL,
    `reason` longtext CHARACTER SET utf8mb4 NULL,
    `remarks` longtext CHARACTER SET utf8mb4 NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_employee_promotions` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `employee_transfers` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `from_department` longtext CHARACTER SET utf8mb4 NULL,
    `to_department` longtext CHARACTER SET utf8mb4 NULL,
    `from_designation` longtext CHARACTER SET utf8mb4 NULL,
    `to_designation` longtext CHARACTER SET utf8mb4 NULL,
    `from_company_id` int NULL,
    `to_company_id` int NULL,
    `from_branch_id` int NULL,
    `to_branch_id` int NULL,
    `effective_date` date NOT NULL,
    `reason` longtext CHARACTER SET utf8mb4 NULL,
    `remarks` longtext CHARACTER SET utf8mb4 NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `approved_by_user_id` int NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_employee_transfers` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `excel_attendances` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `att_date` date NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `hours_worked` decimal(65,30) NULL,
    `company_id` int NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_excel_attendances` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `expense_claims` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `title` longtext CHARACTER SET utf8mb4 NOT NULL,
    `currency` longtext CHARACTER SET utf8mb4 NOT NULL,
    `travel_request_id` int NULL,
    `notes` longtext CHARACTER SET utf8mb4 NULL,
    `total_amount` decimal(65,30) NOT NULL,
    `total_gst` decimal(65,30) NOT NULL,
    `status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `submitted_at` datetime(6) NULL,
    `is_deleted` tinyint(1) NOT NULL,
    `created_by` longtext CHARACTER SET utf8mb4 NULL,
    `updated_by` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NULL,
    CONSTRAINT `PK_expense_claims` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `geofences` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `fence_type` varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Office',
    `latitude` double NOT NULL,
    `longitude` double NOT NULL,
    `radius_metres` double NOT NULL DEFAULT 200.0,
    `branch_id` int NULL,
    `address` longtext CHARACTER SET utf8mb4 NULL,
    `allow_outside_checkin` tinyint(1) NOT NULL DEFAULT FALSE,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    `created_by` varchar(100) CHARACTER SET utf8mb4 NULL,
    `updated_by` varchar(100) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NULL,
    CONSTRAINT `PK_geofences` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `helpdesk_categories` (
    `id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `company_id` int NOT NULL,
    CONSTRAINT `PK_helpdesk_categories` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `holiday_calendars` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `date` date NOT NULL,
    `description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `is_optional` tinyint(1) NOT NULL DEFAULT FALSE,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_holiday_calendars` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `interviews` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `candidate_id` int NOT NULL,
    `job_requisition_id` int NULL,
    `scheduled_at` datetime(6) NOT NULL,
    `interview_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `venue` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `interviewer_names` longtext CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `feedback_score` int NULL,
    `feedback_notes` longtext CHARACTER SET utf8mb4 NOT NULL,
    `recommendation` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_interviews` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `job_requisitions` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `department_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `openings_count` int NOT NULL,
    `experience_required` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `skills_required` longtext CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `job_type` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `min_salary` numeric(18,2) NULL,
    `max_salary` numeric(18,2) NULL,
    `location` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `closing_date` datetime(6) NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_job_requisitions` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `leave_balance_adjustments` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NULL,
    `leave_type_id` int NOT NULL,
    `year` int NOT NULL,
    `days` int NOT NULL,
    `reason` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `adjusted_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_leave_balance_adjustments` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `leave_balances` (
    `balance_id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `employee_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `leave_type_id` int NOT NULL,
    `year` int NOT NULL,
    `total_days` int NOT NULL,
    `available_days` int NOT NULL,
    `used_days` int NOT NULL,
    `pending_days` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NULL,
    CONSTRAINT `PK_leave_balances` PRIMARY KEY (`balance_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `leave_requests` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NULL,
    `leave_type_id` int NOT NULL,
    `start_date` date NOT NULL,
    `end_date` date NOT NULL,
    `total_days` int NOT NULL,
    `reason` longtext CHARACTER SET utf8mb4 NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `approved_by_user_id` int NULL,
    `approver_remarks` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `decided_at` datetime(6) NULL,
    CONSTRAINT `PK_leave_requests` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `leave_types` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `annual_quota_days` int NOT NULL,
    `is_paid` tinyint(1) NOT NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_leave_types` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `notifications` (
    `id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `company_id` int NULL,
    `title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `message` longtext CHARACTER SET utf8mb4 NOT NULL,
    `type` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'info',
    `entity_type` varchar(100) CHARACTER SET utf8mb4 NULL,
    `entity_id` varchar(100) CHARACTER SET utf8mb4 NULL,
    `is_read` tinyint(1) NOT NULL DEFAULT FALSE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `read_at` datetime(6) NULL,
    CONSTRAINT `PK_notifications` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `offer_letters` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `candidate_id` int NOT NULL,
    `job_requisition_id` int NULL,
    `offered_designation` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `offered_department` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `offered_salary` numeric(18,2) NOT NULL,
    `joining_date` datetime(6) NOT NULL,
    `offer_issued_at` datetime(6) NOT NULL,
    `expiry_date` datetime(6) NOT NULL,
    `status` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `approved_by_user_id` int NULL,
    `approval_notes` longtext CHARACTER SET utf8mb4 NOT NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_offer_letters` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `onboarding_templates` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `title` varchar(500) CHARACTER SET utf8mb4 NULL,
    `description` longtext CHARACTER SET utf8mb4 NULL,
    `steps` longtext CHARACTER SET utf8mb4 NOT NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_onboarding_templates` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `password_reset_tokens` (
    `id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `token_hash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `expires_at` datetime(6) NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `used_at` datetime(6) NULL,
    CONSTRAINT `PK_password_reset_tokens` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `payroll_locks` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `month` int NOT NULL,
    `year` int NOT NULL,
    `is_locked` tinyint(1) NOT NULL DEFAULT TRUE,
    `locked_at` datetime(6) NOT NULL,
    `locked_by_user_id` int NOT NULL,
    `unlocked_at` datetime(6) NULL,
    `unlocked_by_user_id` int NULL,
    `notes` varchar(500) CHARACTER SET utf8mb4 NULL,
    `row_version` timestamp(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_payroll_locks` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `payslips` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NOT NULL,
    `month` int NOT NULL,
    `year` int NOT NULL,
    `working_days` int NOT NULL,
    `days_present` int NOT NULL,
    `basic_pay` decimal(14,2) NOT NULL,
    `hra` decimal(14,2) NOT NULL,
    `da` decimal(14,2) NOT NULL,
    `conveyance` decimal(14,2) NOT NULL,
    `medical_allowance` decimal(14,2) NOT NULL,
    `other_allowances` decimal(14,2) NOT NULL,
    `gross_earnings` decimal(14,2) NOT NULL,
    `pf_employee` decimal(14,2) NOT NULL,
    `pf_employer` decimal(14,2) NOT NULL,
    `esi` decimal(14,2) NOT NULL,
    `pt` decimal(14,2) NOT NULL,
    `tds` decimal(14,2) NOT NULL,
    `other_deductions` decimal(14,2) NOT NULL,
    `total_deductions` decimal(14,2) NOT NULL,
    `net_pay` decimal(14,2) NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Generated',
    `row_version` timestamp(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_payslips` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `performance_cycles` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `start_date` datetime(6) NOT NULL,
    `end_date` datetime(6) NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `review_type` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_performance_cycles` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `performance_reviews` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `employee_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `reviewer_id` int NOT NULL,
    `performance_cycle_id` int NULL,
    `review_type` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `self_rating` numeric(3,1) NULL,
    `manager_rating` numeric(3,1) NULL,
    `final_rating` numeric(3,1) NULL,
    `self_comments` longtext CHARACTER SET utf8mb4 NOT NULL,
    `manager_comments` longtext CHARACTER SET utf8mb4 NOT NULL,
    `hr_comments` longtext CHARACTER SET utf8mb4 NOT NULL,
    `overall_comments` longtext CHARACTER SET utf8mb4 NOT NULL,
    `submitted_at` datetime(6) NULL,
    `acknowledged_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_performance_reviews` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `permissions` (
    `id` int NOT NULL AUTO_INCREMENT,
    `role` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `add_employee` tinyint(1) NOT NULL,
    `edit_employee` tinyint(1) NOT NULL,
    `view_employee` tinyint(1) NOT NULL,
    `delete_employee` tinyint(1) NOT NULL,
    `attendance_upload` tinyint(1) NOT NULL,
    `employee_registration` tinyint(1) NOT NULL,
    `view_all_employees` tinyint(1) NOT NULL,
    `company_details` tinyint(1) NOT NULL,
    `web_attendance_view` tinyint(1) NOT NULL,
    `excel_attendance_upload` tinyint(1) NOT NULL,
    `excel_attendance_view` tinyint(1) NOT NULL,
    `payroll_view` tinyint(1) NOT NULL,
    `payroll_generate` tinyint(1) NOT NULL,
    `reports_attendance` tinyint(1) NOT NULL,
    `reports_employee` tinyint(1) NOT NULL,
    `appreciation` tinyint(1) NOT NULL,
    `logo_upload` tinyint(1) NOT NULL,
    `manage_admin_users` tinyint(1) NOT NULL,
    `leave_management` tinyint(1) NOT NULL DEFAULT TRUE,
    `sales_view` tinyint(1) NOT NULL,
    `sales_create` tinyint(1) NOT NULL,
    `sales_edit` tinyint(1) NOT NULL,
    `sales_delete` tinyint(1) NOT NULL,
    `lead_view` tinyint(1) NOT NULL,
    `lead_create` tinyint(1) NOT NULL,
    `lead_edit` tinyint(1) NOT NULL,
    `lead_delete` tinyint(1) NOT NULL,
    `lead_assign` tinyint(1) NOT NULL,
    `lead_reassign` tinyint(1) NOT NULL,
    `lead_view_assigned` tinyint(1) NOT NULL,
    `lead_view_all` tinyint(1) NOT NULL,
    `customer_view` tinyint(1) NOT NULL,
    `customer_create` tinyint(1) NOT NULL,
    `customer_edit` tinyint(1) NOT NULL,
    `customer_delete` tinyint(1) NOT NULL,
    `meeting_view` tinyint(1) NOT NULL,
    `meeting_create` tinyint(1) NOT NULL,
    `meeting_edit` tinyint(1) NOT NULL,
    `meeting_delete` tinyint(1) NOT NULL,
    `visit_view` tinyint(1) NOT NULL,
    `visit_create` tinyint(1) NOT NULL,
    `visit_edit` tinyint(1) NOT NULL,
    `visit_delete` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_permissions` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `refresh_tokens` (
    `id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `token_hash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `expires_at` datetime(6) NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `revoked_at` datetime(6) NULL,
    `replaced_by_token_hash` longtext CHARACTER SET utf8mb4 NULL,
    `mfa_verified` tinyint(1) NOT NULL DEFAULT FALSE,
    CONSTRAINT `PK_refresh_tokens` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `roles` (
    `id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `description` longtext CHARACTER SET utf8mb4 NULL,
    `is_system_role` tinyint(1) NOT NULL DEFAULT FALSE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_roles` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `salary_structures` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NULL,
    `ctc` decimal(14,2) NOT NULL,
    `basic_pay` decimal(14,2) NOT NULL,
    `hra` decimal(14,2) NOT NULL,
    `da` decimal(14,2) NOT NULL,
    `conveyance` decimal(14,2) NOT NULL,
    `medical_allowance` decimal(14,2) NOT NULL,
    `other_allowances` decimal(14,2) NOT NULL,
    `pf_employee` decimal(14,2) NOT NULL,
    `pf_employer` decimal(14,2) NOT NULL,
    `esi` decimal(14,2) NOT NULL,
    `pt` decimal(14,2) NOT NULL,
    `tds` decimal(14,2) NOT NULL,
    `effective_from` date NOT NULL,
    `effective_to` date NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `is_old_regime` tinyint(1) NOT NULL DEFAULT FALSE,
    `section_80c_deduction` decimal(14,2) NOT NULL DEFAULT 0.0,
    CONSTRAINT `PK_salary_structures` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `sales_customers` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `branch_id` int NULL,
    `customer_code` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `gst` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `pan` varchar(15) CHARACTER SET utf8mb4 NOT NULL,
    `company_name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `billing_address` longtext CHARACTER SET utf8mb4 NOT NULL,
    `shipping_address` longtext CHARACTER SET utf8mb4 NOT NULL,
    `contact_person` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `contact_phone` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `contact_email` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `assigned_sales_person_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `sales_lead_id` int NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    CONSTRAINT `PK_sales_customers` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `sales_follow_ups` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `branch_id` int NULL,
    `sales_lead_id` int NOT NULL,
    `notes` longtext CHARACTER SET utf8mb4 NOT NULL,
    `reminder_date` datetime(6) NOT NULL,
    `reminder_time` time(6) NULL,
    `mode` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    CONSTRAINT `PK_sales_follow_ups` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `sales_leads` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `branch_id` int NULL,
    `lead_no` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `title` longtext CHARACTER SET utf8mb4 NOT NULL,
    `company_name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `contact_person` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `mobile` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `email` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `city` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `state` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `country` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `address` longtext CHARACTER SET utf8mb4 NOT NULL,
    `lead_source` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `industry` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `employee_owner_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `priority` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `expected_value` numeric(18,2) NULL,
    `next_follow_up_date` datetime(6) NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    CONSTRAINT `PK_sales_leads` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `sales_meetings` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `branch_id` int NULL,
    `sales_lead_id` int NULL,
    `sales_customer_id` int NULL,
    `title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `meeting_date` datetime(6) NOT NULL,
    `meeting_time` time(6) NOT NULL,
    `location` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `google_map_url` varchar(500) CHARACTER SET utf8mb4 NULL,
    `meeting_type` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `outcome` longtext CHARACTER SET utf8mb4 NOT NULL,
    `notes` longtext CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    CONSTRAINT `PK_sales_meetings` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `sales_quotations` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `branch_id` int NULL,
    `quotation_number` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `sales_lead_id` int NULL,
    `sales_customer_id` int NULL,
    `amount` numeric(18,2) NOT NULL,
    `tax` numeric(18,2) NOT NULL,
    `discount` numeric(18,2) NOT NULL,
    `total_amount` numeric(18,2) NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `valid_until` datetime(6) NULL,
    `notes` longtext CHARACTER SET utf8mb4 NOT NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    CONSTRAINT `PK_sales_quotations` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `sales_tasks` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `branch_id` int NULL,
    `sales_lead_id` int NULL,
    `sales_customer_id` int NULL,
    `title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `assigned_to_employee_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `priority` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `deadline` datetime(6) NULL,
    `reminder_date` datetime(6) NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    CONSTRAINT `PK_sales_tasks` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `sales_visits` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `branch_id` int NULL,
    `sales_lead_id` int NULL,
    `sales_customer_id` int NULL,
    `visited_employee_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `check_in_latitude` numeric(10,7) NULL,
    `check_in_longitude` numeric(10,7) NULL,
    `check_in_address` longtext CHARACTER SET utf8mb4 NOT NULL,
    `check_in_photo_path` varchar(500) CHARACTER SET utf8mb4 NULL,
    `check_in_time` datetime(6) NULL,
    `check_out_time` datetime(6) NULL,
    `duration_minutes` int NULL,
    `distance_km` numeric(10,2) NULL,
    `notes` longtext CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `created_by_user_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    CONSTRAINT `PK_sales_visits` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `shifts` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `shift_name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `start_time` time(6) NOT NULL,
    `end_time` time(6) NOT NULL,
    `grace_period_minutes` int NOT NULL DEFAULT 15,
    `late_threshold_minutes` int NOT NULL DEFAULT 0,
    `half_day_threshold_hours` decimal(4,1) NOT NULL DEFAULT 4.0,
    `early_exit_threshold_minutes` int NOT NULL DEFAULT 60,
    `is_night_shift` tinyint(1) NOT NULL DEFAULT FALSE,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_shifts` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `timesheet_entries` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `employee_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `work_date` date NOT NULL,
    `project_code` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `task_description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `hours_worked` numeric(5,2) NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Draft',
    `manager_remarks` longtext CHARACTER SET utf8mb4 NULL,
    `approved_by_user_id` int NULL,
    `approved_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_timesheet_entries` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `timesheets` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NULL,
    `week_start_date` date NOT NULL,
    `week_end_date` date NOT NULL,
    `total_hours` numeric(6,2) NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Draft',
    `manager_remarks` longtext CHARACTER SET utf8mb4 NULL,
    `approved_by_user_id` int NULL,
    `approved_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_timesheets` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `training_programs` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `title` longtext CHARACTER SET utf8mb4 NOT NULL,
    `description` longtext CHARACTER SET utf8mb4 NULL,
    `start_date` datetime(6) NOT NULL,
    `end_date` datetime(6) NOT NULL,
    `trainer` longtext CHARACTER SET utf8mb4 NULL,
    `max_seats` int NOT NULL,
    `is_active` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `deleted_at` datetime(6) NULL,
    CONSTRAINT `PK_training_programs` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `travel_requests` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `travel_type` longtext CHARACTER SET utf8mb4 NOT NULL,
    `purpose` longtext CHARACTER SET utf8mb4 NOT NULL,
    `from_city` longtext CHARACTER SET utf8mb4 NOT NULL,
    `to_city` longtext CHARACTER SET utf8mb4 NOT NULL,
    `start_date` datetime(6) NOT NULL,
    `end_date` datetime(6) NOT NULL,
    `mode_of_travel` longtext CHARACTER SET utf8mb4 NOT NULL,
    `advance_required` tinyint(1) NOT NULL,
    `advance_amount` decimal(65,30) NOT NULL,
    `estimated_cost` decimal(65,30) NOT NULL,
    `notes` longtext CHARACTER SET utf8mb4 NULL,
    `attachment_path` longtext CHARACTER SET utf8mb4 NULL,
    `status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `approved_by` int NULL,
    `is_deleted` tinyint(1) NOT NULL,
    `created_by` longtext CHARACTER SET utf8mb4 NULL,
    `updated_by` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NULL,
    CONSTRAINT `PK_travel_requests` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `users` (
    `id` int NOT NULL AUTO_INCREMENT,
    `email` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `password_hash` longtext CHARACTER SET utf8mb4 NOT NULL,
    `role` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `full_name` varchar(255) CHARACTER SET utf8mb4 NULL,
    `admin_role` varchar(50) CHARACTER SET utf8mb4 NULL,
    `company_id` int NULL,
    `profile_picture_path` varchar(500) CHARACTER SET utf8mb4 NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    `deleted_at` datetime(6) NULL,
    `must_change_password` tinyint(1) NOT NULL DEFAULT FALSE,
    `failed_login_attempts` int NOT NULL DEFAULT 0,
    `lockout_until` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `totp_secret` varchar(500) CHARACTER SET utf8mb4 NULL,
    `is_mfa_enabled` tinyint(1) NOT NULL DEFAULT FALSE,
    CONSTRAINT `PK_users` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `web_attendances` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `company_id` int NULL,
    `att_date` date NOT NULL,
    `check_in` time(6) NULL,
    `check_out` time(6) NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `admin_edit_reason` varchar(500) CHARACTER SET utf8mb4 NULL,
    `overtime_minutes` int NOT NULL,
    `is_deleted` tinyint(1) NOT NULL,
    `deleted_at` datetime(6) NULL,
    `deleted_reason` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_web_attendances` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `webhook_outbox` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `subscription_id` int NOT NULL,
    `event_type` longtext CHARACTER SET utf8mb4 NOT NULL,
    `target_url` longtext CHARACTER SET utf8mb4 NOT NULL,
    `payload` longtext CHARACTER SET utf8mb4 NOT NULL,
    `signature` longtext CHARACTER SET utf8mb4 NULL,
    `status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `attempt_count` int NOT NULL,
    `last_attempt_at` datetime(6) NULL,
    `last_error` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `sent_at` datetime(6) NULL,
    CONSTRAINT `PK_webhook_outbox` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `webhook_subscriptions` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `event_type` longtext CHARACTER SET utf8mb4 NOT NULL,
    `target_url` longtext CHARACTER SET utf8mb4 NOT NULL,
    `secret` longtext CHARACTER SET utf8mb4 NOT NULL,
    `is_active` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_webhook_subscriptions` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `assets` (
    `id` int NOT NULL AUTO_INCREMENT,
    `asset_code` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `category_id` int NULL,
    `serial_number` varchar(100) CHARACTER SET utf8mb4 NULL,
    `purchase_date` datetime(6) NULL,
    `purchase_price` decimal(18,2) NULL,
    `current_value` decimal(18,2) NULL,
    `status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Available',
    `location` varchar(200) CHARACTER SET utf8mb4 NULL,
    `assigned_to_employee_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `assigned_at` datetime(6) NULL,
    `company_id` int NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    `deleted_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_assets` PRIMARY KEY (`id`),
    CONSTRAINT `FK_assets_asset_categories_category_id` FOREIGN KEY (`category_id`) REFERENCES `asset_categories` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `biometric_logs` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `device_id` int NOT NULL,
    `employee_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `punch_time` datetime(6) NOT NULL,
    `direction` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `device_serial` varchar(100) CHARACTER SET utf8mb4 NULL,
    `is_processed` tinyint(1) NOT NULL,
    `web_attendance_id` int NULL,
    `skip_reason` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_biometric_logs` PRIMARY KEY (`id`),
    CONSTRAINT `FK_biometric_logs_biometric_devices_device_id` FOREIGN KEY (`device_id`) REFERENCES `biometric_devices` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `biometric_sync_histories` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `device_id` int NULL,
    `vendor_name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `range_from` datetime(6) NOT NULL,
    `range_to` datetime(6) NOT NULL,
    `started_at` datetime(6) NOT NULL,
    `completed_at` datetime(6) NULL,
    `total_fetched` int NOT NULL,
    `logs_created` int NOT NULL,
    `logs_updated` int NOT NULL,
    `logs_skipped` int NOT NULL,
    `status` tinyint(1) NOT NULL,
    `error_message` longtext CHARACTER SET utf8mb4 NULL,
    `is_auto_sync` tinyint(1) NOT NULL,
    `triggered_by_user_id` int NULL,
    CONSTRAINT `PK_biometric_sync_histories` PRIMARY KEY (`id`),
    CONSTRAINT `FK_biometric_sync_histories_biometric_devices_device_id` FOREIGN KEY (`device_id`) REFERENCES `biometric_devices` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `company_branches` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `branch_name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `address_line1` longtext CHARACTER SET utf8mb4 NULL,
    `address_line2` longtext CHARACTER SET utf8mb4 NULL,
    `city` longtext CHARACTER SET utf8mb4 NULL,
    `state_province` longtext CHARACTER SET utf8mb4 NULL,
    `country` longtext CHARACTER SET utf8mb4 NULL,
    `postal_code` longtext CHARACTER SET utf8mb4 NULL,
    `phone_number` longtext CHARACTER SET utf8mb4 NULL,
    `email` longtext CHARACTER SET utf8mb4 NULL,
    `branch_manager_name` longtext CHARACTER SET utf8mb4 NULL,
    `is_head_office` tinyint(1) NOT NULL DEFAULT FALSE,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_company_branches` PRIMARY KEY (`id`),
    CONSTRAINT `FK_company_branches_companies_company_id` FOREIGN KEY (`company_id`) REFERENCES `companies` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `company_settings` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `working_days_per_month` int NOT NULL DEFAULT 26,
    `pf_percentage` decimal(5,2) NOT NULL DEFAULT 12.0,
    `esi_percentage` decimal(5,2) NOT NULL DEFAULT 0.75,
    `pt_amount` decimal(10,2) NOT NULL DEFAULT 200.0,
    `payslip_footer_note` longtext CHARACTER SET utf8mb4 NULL,
    `time_zone` longtext CHARACTER SET utf8mb4 NULL DEFAULT ('Asia/Kolkata'),
    `check_in_time` time(6) NULL,
    `check_out_time` time(6) NULL,
    `overtime_threshold_minutes` int NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_company_settings` PRIMARY KEY (`id`),
    CONSTRAINT `FK_company_settings_companies_company_id` FOREIGN KEY (`company_id`) REFERENCES `companies` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `employees` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `user_id` int NULL,
    `company_id` int NOT NULL,
    `first_name` longtext CHARACTER SET utf8mb4 NULL,
    `last_name` longtext CHARACTER SET utf8mb4 NULL,
    `full_name` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `gender` varchar(20) CHARACTER SET utf8mb4 NULL,
    `date_of_birth` date NULL,
    `nationality` varchar(100) CHARACTER SET utf8mb4 NULL,
    `marital_status` varchar(50) CHARACTER SET utf8mb4 NULL,
    `blood_group` varchar(10) CHARACTER SET utf8mb4 NULL,
    `permanent_address` longtext CHARACTER SET utf8mb4 NULL,
    `current_address` longtext CHARACTER SET utf8mb4 NULL,
    `aadhaar` varchar(500) CHARACTER SET utf8mb4 NULL,
    `pan` varchar(500) CHARACTER SET utf8mb4 NULL,
    `identity_docs` longtext CHARACTER SET utf8mb4 NULL,
    `medical_conditions` longtext CHARACTER SET utf8mb4 NULL,
    `hobbies` longtext CHARACTER SET utf8mb4 NULL,
    `languages` longtext CHARACTER SET utf8mb4 NULL,
    `email` longtext CHARACTER SET utf8mb4 NULL,
    `phone_number` longtext CHARACTER SET utf8mb4 NULL,
    `date_of_joining` date NULL,
    `designation` varchar(200) CHARACTER SET utf8mb4 NULL,
    `department` varchar(200) CHARACTER SET utf8mb4 NULL,
    `skills` longtext CHARACTER SET utf8mb4 NULL,
    `responsibilities` longtext CHARACTER SET utf8mb4 NULL,
    `status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `department_id` int NULL,
    `bank_account_holder` varchar(500) CHARACTER SET utf8mb4 NULL,
    `bank_name` varchar(500) CHARACTER SET utf8mb4 NULL,
    `branch_name` varchar(500) CHARACTER SET utf8mb4 NULL,
    `account_number` varchar(500) CHARACTER SET utf8mb4 NULL,
    `ifsc_code` varchar(500) CHARACTER SET utf8mb4 NULL,
    `uan` varchar(500) CHARACTER SET utf8mb4 NULL,
    `qualification` varchar(200) CHARACTER SET utf8mb4 NULL,
    `institution` varchar(200) CHARACTER SET utf8mb4 NULL,
    `year_of_passing` int NULL,
    `specialization` varchar(200) CHARACTER SET utf8mb4 NULL,
    `educational_docs` longtext CHARACTER SET utf8mb4 NULL,
    `passport_photo` longtext CHARACTER SET utf8mb4 NULL,
    `previous_employer` varchar(200) CHARACTER SET utf8mb4 NULL,
    `job_title` varchar(200) CHARACTER SET utf8mb4 NULL,
    `duration` varchar(100) CHARACTER SET utf8mb4 NULL,
    `exp_responsibilities` longtext CHARACTER SET utf8mb4 NULL,
    `experience_docs` longtext CHARACTER SET utf8mb4 NULL,
    `emergency_contact_name` varchar(200) CHARACTER SET utf8mb4 NULL,
    `emergency_contact_relationship` varchar(100) CHARACTER SET utf8mb4 NULL,
    `emergency_contact_phone` varchar(50) CHARACTER SET utf8mb4 NULL,
    `emergency_contact_address` longtext CHARACTER SET utf8mb4 NULL,
    `shift_id` int NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT TRUE,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_employees` PRIMARY KEY (`id`),
    CONSTRAINT `FK_employees_departments_department_id` FOREIGN KEY (`department_id`) REFERENCES `departments` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `expense_approvals` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `expense_claim_id` int NOT NULL,
    `step` longtext CHARACTER SET utf8mb4 NOT NULL,
    `status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `approver_id` int NULL,
    `approver_name` longtext CHARACTER SET utf8mb4 NULL,
    `comments` longtext CHARACTER SET utf8mb4 NULL,
    `action_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_expense_approvals` PRIMARY KEY (`id`),
    CONSTRAINT `FK_expense_approvals_expense_claims_expense_claim_id` FOREIGN KEY (`expense_claim_id`) REFERENCES `expense_claims` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `expense_attachments` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `expense_claim_id` int NOT NULL,
    `file_name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `file_path` longtext CHARACTER SET utf8mb4 NOT NULL,
    `content_type` longtext CHARACTER SET utf8mb4 NULL,
    `file_size_bytes` bigint NOT NULL,
    `uploaded_by` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_expense_attachments` PRIMARY KEY (`id`),
    CONSTRAINT `FK_expense_attachments_expense_claims_expense_claim_id` FOREIGN KEY (`expense_claim_id`) REFERENCES `expense_claims` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `expense_history` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `expense_claim_id` int NOT NULL,
    `action` longtext CHARACTER SET utf8mb4 NOT NULL,
    `previous_status` longtext CHARACTER SET utf8mb4 NULL,
    `new_status` longtext CHARACTER SET utf8mb4 NULL,
    `performed_by` longtext CHARACTER SET utf8mb4 NULL,
    `performed_by_name` longtext CHARACTER SET utf8mb4 NULL,
    `remarks` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_expense_history` PRIMARY KEY (`id`),
    CONSTRAINT `FK_expense_history_expense_claims_expense_claim_id` FOREIGN KEY (`expense_claim_id`) REFERENCES `expense_claims` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `expense_items` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `expense_claim_id` int NOT NULL,
    `category` longtext CHARACTER SET utf8mb4 NOT NULL,
    `description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `amount` decimal(65,30) NOT NULL,
    `gst_amount` decimal(65,30) NOT NULL,
    `currency` longtext CHARACTER SET utf8mb4 NOT NULL,
    `expense_date` date NOT NULL,
    `receipt_path` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_expense_items` PRIMARY KEY (`id`),
    CONSTRAINT `FK_expense_items_expense_claims_expense_claim_id` FOREIGN KEY (`expense_claim_id`) REFERENCES `expense_claims` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `attendance_gps` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `web_attendance_id` int NOT NULL,
    `employee_id` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `latitude` double NOT NULL,
    `longitude` double NOT NULL,
    `accuracy` double NULL,
    `event_type` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'CheckIn',
    `timestamp` datetime(6) NOT NULL,
    `geofence_id` int NULL,
    `distance_metres` double NULL,
    `is_inside_geofence` tinyint(1) NOT NULL,
    `device_type` varchar(50) CHARACTER SET utf8mb4 NULL,
    `browser` varchar(200) CHARACTER SET utf8mb4 NULL,
    `ip_address` varchar(50) CHARACTER SET utf8mb4 NULL,
    `network` varchar(30) CHARACTER SET utf8mb4 NULL,
    `battery_level` double NULL,
    `gps_status` varchar(30) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_attendance_gps` PRIMARY KEY (`id`),
    CONSTRAINT `FK_attendance_gps_geofences_geofence_id` FOREIGN KEY (`geofence_id`) REFERENCES `geofences` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `geofence_history` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `geofence_id` int NOT NULL,
    `action` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `changed_by` varchar(100) CHARACTER SET utf8mb4 NULL,
    `change_details` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_geofence_history` PRIMARY KEY (`id`),
    CONSTRAINT `FK_geofence_history_geofences_geofence_id` FOREIGN KEY (`geofence_id`) REFERENCES `geofences` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `helpdesk_tickets` (
    `id` int NOT NULL AUTO_INCREMENT,
    `title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(5000) CHARACTER SET utf8mb4 NULL,
    `status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Open',
    `priority` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Medium',
    `category_id` int NULL,
    `raised_by_employee_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `assigned_to_user_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `company_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `updated_at` datetime(6) NOT NULL,
    `resolved_at` datetime(6) NULL,
    `deleted_at` datetime(6) NULL,
    CONSTRAINT `PK_helpdesk_tickets` PRIMARY KEY (`id`),
    CONSTRAINT `FK_helpdesk_tickets_helpdesk_categories_category_id` FOREIGN KEY (`category_id`) REFERENCES `helpdesk_categories` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `onboarding_records` (
    `id` int NOT NULL AUTO_INCREMENT,
    `employee_id` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `employee_fk` int NULL,
    `template_id` int NOT NULL,
    `completed_steps` longtext CHARACTER SET utf8mb4 NOT NULL,
    `assigned_to` int NULL,
    `due_date` datetime(6) NULL,
    `completed_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `deleted_at` datetime(6) NULL,
    CONSTRAINT `PK_onboarding_records` PRIMARY KEY (`id`),
    CONSTRAINT `FK_onboarding_records_onboarding_templates_template_id` FOREIGN KEY (`template_id`) REFERENCES `onboarding_templates` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `sales_lead_assignments` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NOT NULL,
    `sales_lead_id` int NOT NULL,
    `assigned_to_employee_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `assigned_by_user_id` int NOT NULL,
    `reassigned_from_employee_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `action_type` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Assigned',
    `remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `assigned_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT FALSE,
    `assigned_to_employee_fk` int NULL,
    `reassigned_from_employee_fk` int NULL,
    CONSTRAINT `PK_sales_lead_assignments` PRIMARY KEY (`id`),
    CONSTRAINT `FK_sales_lead_assignments_sales_leads_sales_lead_id` FOREIGN KEY (`sales_lead_id`) REFERENCES `sales_leads` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `training_enrollments` (
    `id` int NOT NULL AUTO_INCREMENT,
    `training_program_id` int NOT NULL,
    `employee_id` longtext CHARACTER SET utf8mb4 NOT NULL,
    `status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `completion_date` datetime(6) NULL,
    `certificate_path` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_training_enrollments` PRIMARY KEY (`id`),
    CONSTRAINT `FK_training_enrollments_training_programs_training_program_id` FOREIGN KEY (`training_program_id`) REFERENCES `training_programs` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `travel_approvals` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `travel_request_id` int NOT NULL,
    `step` longtext CHARACTER SET utf8mb4 NOT NULL,
    `status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `approver_id` int NULL,
    `approver_name` longtext CHARACTER SET utf8mb4 NULL,
    `comments` longtext CHARACTER SET utf8mb4 NULL,
    `action_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_travel_approvals` PRIMARY KEY (`id`),
    CONSTRAINT `FK_travel_approvals_travel_requests_travel_request_id` FOREIGN KEY (`travel_request_id`) REFERENCES `travel_requests` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `travel_history` (
    `id` int NOT NULL AUTO_INCREMENT,
    `company_id` int NULL,
    `travel_request_id` int NOT NULL,
    `action` longtext CHARACTER SET utf8mb4 NOT NULL,
    `previous_status` longtext CHARACTER SET utf8mb4 NULL,
    `new_status` longtext CHARACTER SET utf8mb4 NULL,
    `performed_by` longtext CHARACTER SET utf8mb4 NULL,
    `performed_by_name` longtext CHARACTER SET utf8mb4 NULL,
    `remarks` longtext CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_travel_history` PRIMARY KEY (`id`),
    CONSTRAINT `FK_travel_history_travel_requests_travel_request_id` FOREIGN KEY (`travel_request_id`) REFERENCES `travel_requests` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `asset_history` (
    `id` int NOT NULL AUTO_INCREMENT,
    `asset_id` int NOT NULL,
    `action` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `employee_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `notes` varchar(500) CHARACTER SET utf8mb4 NULL,
    `performed_by_user_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `timestamp` datetime(6) NOT NULL,
    CONSTRAINT `PK_asset_history` PRIMARY KEY (`id`),
    CONSTRAINT `FK_asset_history_assets_asset_id` FOREIGN KEY (`asset_id`) REFERENCES `assets` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `helpdesk_comments` (
    `id` int NOT NULL AUTO_INCREMENT,
    `ticket_id` int NOT NULL,
    `author_id` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `message` varchar(5000) CHARACTER SET utf8mb4 NOT NULL,
    `is_internal` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_helpdesk_comments` PRIMARY KEY (`id`),
    CONSTRAINT `FK_helpdesk_comments_helpdesk_tickets_ticket_id` FOREIGN KEY (`ticket_id`) REFERENCES `helpdesk_tickets` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `helpdesk_history` (
    `id` int NOT NULL AUTO_INCREMENT,
    `ticket_id` int NOT NULL,
    `action` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `old_value` varchar(200) CHARACTER SET utf8mb4 NULL,
    `new_value` varchar(200) CHARACTER SET utf8mb4 NULL,
    `performed_by_user_id` varchar(50) CHARACTER SET utf8mb4 NULL,
    `timestamp` datetime(6) NOT NULL,
    CONSTRAINT `PK_helpdesk_history` PRIMARY KEY (`id`),
    CONSTRAINT `FK_helpdesk_history_helpdesk_tickets_ticket_id` FOREIGN KEY (`ticket_id`) REFERENCES `helpdesk_tickets` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

INSERT INTO `leave_types` (`id`, `annual_quota_days`, `company_id`, `created_at`, `is_active`, `is_paid`, `name`)
VALUES (1, 12, NULL, TIMESTAMP '2024-01-01 00:00:00', TRUE, TRUE, 'Casual Leave'),
(2, 8, NULL, TIMESTAMP '2024-01-01 00:00:00', TRUE, TRUE, 'Sick Leave'),
(3, 15, NULL, TIMESTAMP '2024-01-01 00:00:00', TRUE, TRUE, 'Earned Leave');

CREATE INDEX `IX_asset_categories_company_id_name` ON `asset_categories` (`company_id`, `name`);

CREATE INDEX `IX_asset_history_asset_id` ON `asset_history` (`asset_id`);

CREATE INDEX `IX_assets_category_id` ON `assets` (`category_id`);

CREATE UNIQUE INDEX `IX_assets_company_id_asset_code` ON `assets` (`company_id`, `asset_code`);

CREATE INDEX `IX_assets_company_id_status` ON `assets` (`company_id`, `status`);

CREATE INDEX `IX_attendance_devices_employee_id_device_fingerprint` ON `attendance_devices` (`employee_id`, `device_fingerprint`);

CREATE INDEX `IX_attendance_gps_company_id` ON `attendance_gps` (`company_id`);

CREATE INDEX `IX_attendance_gps_employee_id` ON `attendance_gps` (`employee_id`);

CREATE INDEX `IX_attendance_gps_geofence_id` ON `attendance_gps` (`geofence_id`);

CREATE INDEX `IX_attendance_gps_timestamp` ON `attendance_gps` (`timestamp`);

CREATE INDEX `IX_attendance_location_audit_company_id_employee_id` ON `attendance_location_audit` (`company_id`, `employee_id`);

CREATE INDEX `IX_attendance_location_audit_created_at` ON `attendance_location_audit` (`created_at`);

CREATE INDEX `IX_audit_logs_action` ON `audit_logs` (`action`);

CREATE INDEX `IX_audit_logs_occurred_at` ON `audit_logs` (`occurred_at`);

CREATE INDEX `IX_audit_logs_performed_by` ON `audit_logs` (`performed_by`);

CREATE INDEX `ix_biometric_devices_company_id` ON `biometric_devices` (`company_id`);

CREATE UNIQUE INDEX `ix_biometric_devices_company_ip_port` ON `biometric_devices` (`company_id`, `ip_address`, `port`);

CREATE INDEX `ix_biometric_logs_company_id` ON `biometric_logs` (`company_id`);

CREATE INDEX `ix_biometric_logs_company_processed` ON `biometric_logs` (`company_id`, `is_processed`);

CREATE INDEX `ix_biometric_logs_device_id` ON `biometric_logs` (`device_id`);

CREATE INDEX `ix_biometric_logs_employee_punch_time` ON `biometric_logs` (`employee_id`, `punch_time`);

CREATE UNIQUE INDEX `ix_biometric_settings_company_id` ON `biometric_settings` (`company_id`);

CREATE INDEX `ix_biometric_sync_histories_company_id` ON `biometric_sync_histories` (`company_id`);

CREATE INDEX `ix_biometric_sync_histories_device_id` ON `biometric_sync_histories` (`device_id`);

CREATE INDEX `ix_biometric_sync_histories_started_at` ON `biometric_sync_histories` (`started_at`);

CREATE INDEX `ix_bonuses_company_id` ON `bonuses` (`company_id`);

CREATE INDEX `IX_candidates_company_id` ON `candidates` (`company_id`);

CREATE INDEX `IX_company_branches_company_id` ON `company_branches` (`company_id`);

CREATE UNIQUE INDEX `IX_company_settings_company_id` ON `company_settings` (`company_id`);

CREATE INDEX `IX_continuous_feedback_company_id_to_employee_id` ON `continuous_feedback` (`company_id`, `to_employee_id`);

CREATE INDEX `ix_deductions_company_id` ON `deductions` (`company_id`);

CREATE INDEX `IX_employee_goals_company_id_employee_id` ON `employee_goals` (`company_id`, `employee_id`);

CREATE INDEX `ix_employees_company_id` ON `employees` (`company_id`);

CREATE INDEX `IX_employees_department_id` ON `employees` (`department_id`);

CREATE UNIQUE INDEX `IX_employees_employee_id` ON `employees` (`employee_id`);

CREATE INDEX `ix_employees_shift_id` ON `employees` (`shift_id`);

CREATE INDEX `ix_excel_attendances_att_date` ON `excel_attendances` (`att_date`);

CREATE INDEX `ix_excel_attendances_company_id` ON `excel_attendances` (`company_id`);

CREATE INDEX `ix_excel_attendances_employee_id` ON `excel_attendances` (`employee_id`);

CREATE INDEX `IX_expense_approvals_expense_claim_id` ON `expense_approvals` (`expense_claim_id`);

CREATE INDEX `IX_expense_attachments_expense_claim_id` ON `expense_attachments` (`expense_claim_id`);

CREATE INDEX `IX_expense_history_expense_claim_id` ON `expense_history` (`expense_claim_id`);

CREATE INDEX `IX_expense_items_expense_claim_id` ON `expense_items` (`expense_claim_id`);

CREATE INDEX `IX_geofence_history_geofence_id` ON `geofence_history` (`geofence_id`);

CREATE INDEX `IX_geofences_company_id` ON `geofences` (`company_id`);

CREATE INDEX `IX_geofences_company_id_is_active` ON `geofences` (`company_id`, `is_active`);

CREATE INDEX `IX_helpdesk_categories_company_id_name` ON `helpdesk_categories` (`company_id`, `name`);

CREATE INDEX `IX_helpdesk_comments_ticket_id` ON `helpdesk_comments` (`ticket_id`);

CREATE INDEX `IX_helpdesk_history_ticket_id` ON `helpdesk_history` (`ticket_id`);

CREATE INDEX `IX_helpdesk_tickets_assigned_to_user_id` ON `helpdesk_tickets` (`assigned_to_user_id`);

CREATE INDEX `IX_helpdesk_tickets_category_id` ON `helpdesk_tickets` (`category_id`);

CREATE INDEX `IX_helpdesk_tickets_company_id_priority` ON `helpdesk_tickets` (`company_id`, `priority`);

CREATE INDEX `IX_helpdesk_tickets_company_id_status` ON `helpdesk_tickets` (`company_id`, `status`);

CREATE INDEX `IX_helpdesk_tickets_raised_by_employee_id` ON `helpdesk_tickets` (`raised_by_employee_id`);

CREATE INDEX `IX_holiday_calendars_company_id_date` ON `holiday_calendars` (`company_id`, `date`);

CREATE INDEX `IX_interviews_company_id` ON `interviews` (`company_id`);

CREATE INDEX `IX_job_requisitions_company_id` ON `job_requisitions` (`company_id`);

CREATE INDEX `IX_leave_balance_adjustments_employee_id_leave_type_id_year` ON `leave_balance_adjustments` (`employee_id`, `leave_type_id`, `year`);

CREATE UNIQUE INDEX `IX_leave_balances_employee_id_leave_type_id_year` ON `leave_balances` (`employee_id`, `leave_type_id`, `year`);

CREATE INDEX `IX_leave_requests_employee_id_status` ON `leave_requests` (`employee_id`, `status`);

CREATE INDEX `IX_notifications_company_id_user_id_is_read` ON `notifications` (`company_id`, `user_id`, `is_read`);

CREATE INDEX `IX_offer_letters_company_id` ON `offer_letters` (`company_id`);

CREATE INDEX `IX_onboarding_records_template_id` ON `onboarding_records` (`template_id`);

CREATE UNIQUE INDEX `IX_password_reset_tokens_token_hash` ON `password_reset_tokens` (`token_hash`);

CREATE UNIQUE INDEX `IX_payroll_locks_company_id_month_year` ON `payroll_locks` (`company_id`, `month`, `year`);

CREATE INDEX `ix_payslips_company_id` ON `payslips` (`company_id`);

CREATE UNIQUE INDEX `IX_payslips_company_id_employee_id_month_year` ON `payslips` (`company_id`, `employee_id`, `month`, `year`);

CREATE INDEX `IX_performance_cycles_company_id` ON `performance_cycles` (`company_id`);

CREATE INDEX `IX_performance_reviews_company_id_employee_id` ON `performance_reviews` (`company_id`, `employee_id`);

CREATE UNIQUE INDEX `IX_permissions_role` ON `permissions` (`role`);

CREATE UNIQUE INDEX `IX_refresh_tokens_token_hash` ON `refresh_tokens` (`token_hash`);

CREATE UNIQUE INDEX `IX_roles_name` ON `roles` (`name`);

CREATE INDEX `ix_salary_structures_company_id` ON `salary_structures` (`company_id`);

CREATE INDEX `ix_sales_lead_assignments_company_lead` ON `sales_lead_assignments` (`company_id`, `sales_lead_id`);

CREATE INDEX `ix_sales_lead_assignments_employee` ON `sales_lead_assignments` (`assigned_to_employee_id`);

CREATE INDEX `IX_sales_lead_assignments_sales_lead_id` ON `sales_lead_assignments` (`sales_lead_id`);

CREATE INDEX `ix_sales_leads_company_status` ON `sales_leads` (`company_id`, `status`);

CREATE INDEX `ix_shifts_company_id` ON `shifts` (`company_id`);

CREATE INDEX `ix_timesheets_company_id` ON `timesheets` (`company_id`);

CREATE INDEX `IX_timesheets_employee_id_week_start_date` ON `timesheets` (`employee_id`, `week_start_date`);

CREATE INDEX `IX_training_enrollments_training_program_id` ON `training_enrollments` (`training_program_id`);

CREATE INDEX `IX_travel_approvals_travel_request_id` ON `travel_approvals` (`travel_request_id`);

CREATE INDEX `IX_travel_history_travel_request_id` ON `travel_history` (`travel_request_id`);

CREATE INDEX `ix_users_company_id` ON `users` (`company_id`);

CREATE UNIQUE INDEX `IX_users_email` ON `users` (`email`);

CREATE INDEX `ix_web_attendances_att_date` ON `web_attendances` (`att_date`);

CREATE INDEX `ix_web_attendances_company_id` ON `web_attendances` (`company_id`);

CREATE UNIQUE INDEX `ux_attendance_employee_date` ON `web_attendances` (`employee_id`, `att_date`);

CREATE INDEX `ix_webhook_subscriptions_company_id` ON `webhook_subscriptions` (`company_id`);

CREATE INDEX `ix_webhook_subscriptions_is_active` ON `webhook_subscriptions` (`is_active`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260810080843_MySqlBaselineSchema', '8.0.8');

COMMIT;

START TRANSACTION;

ALTER TABLE `payslips` ADD CONSTRAINT `fk_payslips_company_id` FOREIGN KEY (`company_id`) REFERENCES `companies` (`id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260810101800_AddPayslipsCompanyForeignKey', '8.0.8');

COMMIT;

