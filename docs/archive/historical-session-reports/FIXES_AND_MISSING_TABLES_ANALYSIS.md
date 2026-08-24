# HRMS Database Fixes & Missing Tables Analysis

## 🔧 FIXES APPLIED

### Fix #1: PII Encryption (Migration 20260812093000)
**Purpose:** Encrypt sensitive employee & customer data (GDPR compliant)

**Tables Modified: 2**

#### ✅ employees table
```sql
ALTER TABLE employees ADD COLUMN is_aadhaar_encrypted BOOL DEFAULT FALSE;
ALTER TABLE employees ADD COLUMN is_pan_encrypted BOOL DEFAULT FALSE;
ALTER TABLE employees ADD COLUMN is_bank_account_encrypted BOOL DEFAULT FALSE;
ALTER TABLE employees ADD COLUMN is_uan_encrypted BOOL DEFAULT FALSE;
ALTER TABLE employees ADD COLUMN is_ifsc_encrypted BOOL DEFAULT FALSE;
ALTER TABLE employees ADD COLUMN pii_encrypted_at DATETIME(6) NULL;
ALTER TABLE employees ADD COLUMN pii_encryption_version VARCHAR(10) NULL;

CREATE INDEX ix_employees_pii_encrypted_flags 
  ON employees(is_aadhaar_encrypted, is_pan_encrypted, is_bank_account_encrypted);
```

**Fields Protected:**
- Aadhaar (unique national ID)
- PAN (tax ID)
- BankAccountNumber
- UAN (provident fund)
- IFSC (bank code)

#### ✅ sales_customers table
```sql
ALTER TABLE sales_customers ADD COLUMN is_gst_encrypted BOOL DEFAULT FALSE;
ALTER TABLE sales_customers ADD COLUMN is_pan_encrypted BOOL DEFAULT FALSE;
ALTER TABLE sales_customers ADD COLUMN pii_encrypted_at DATETIME(6) NULL;
ALTER TABLE sales_customers ADD COLUMN pii_encryption_version VARCHAR(10) NULL;

CREATE INDEX ix_sales_customers_pii_encrypted_flags 
  ON sales_customers(is_gst_encrypted, is_pan_encrypted);
```

**Fields Protected:**
- GST (tax ID)
- PAN (tax ID)

---

### Fix #2: Soft Deletes for 10 Entities (Migration 20260812094000)
**Purpose:** Support soft deletes (GDPR Right to be Forgotten + audit trail)

**Tables Modified: 10**

#### ✅ sales_leads
```sql
ALTER TABLE sales_leads ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_sales_leads_company_deleted ON sales_leads(company_id, deleted_at);
```

#### ✅ sales_customers
```sql
ALTER TABLE sales_customers ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_sales_customers_company_deleted ON sales_customers(company_id, deleted_at);
```

#### ✅ sales_follow_ups
```sql
ALTER TABLE sales_follow_ups ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_sales_follow_ups_company_deleted ON sales_follow_ups(company_id, deleted_at);
```

#### ✅ sales_meetings
```sql
ALTER TABLE sales_meetings ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_sales_meetings_company_deleted ON sales_meetings(company_id, deleted_at);
```

#### ✅ sales_visits
```sql
ALTER TABLE sales_visits ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_sales_visits_company_deleted ON sales_visits(company_id, deleted_at);
```

#### ✅ sales_tasks
```sql
ALTER TABLE sales_tasks ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_sales_tasks_company_deleted ON sales_tasks(company_id, deleted_at);
```

#### ✅ sales_quotations
```sql
ALTER TABLE sales_quotations ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_sales_quotations_company_deleted ON sales_quotations(company_id, deleted_at);
```

#### ✅ sales_lead_assignments
```sql
ALTER TABLE sales_lead_assignments ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_sales_lead_assignments_company_deleted ON sales_lead_assignments(company_id, deleted_at);
```

#### ✅ travel_requests
```sql
ALTER TABLE travel_requests ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_travel_requests_company_deleted ON travel_requests(company_id, deleted_at);
```

#### ✅ expense_claims
```sql
ALTER TABLE expense_claims ADD COLUMN deleted_at DATETIME(6) NULL;
CREATE INDEX ix_expense_claims_company_deleted ON expense_claims(company_id, deleted_at);
```

---

## 📊 Potentially Missing Tables (Recommendations)

Based on HRMS functionality analysis, the following tables would be beneficial:

### HIGH PRIORITY (Should Add)

#### 1. **document_templates** (for document management)
```sql
CREATE TABLE document_templates (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    name VARCHAR(200) NOT NULL UNIQUE,
    description TEXT,
    category VARCHAR(50),  -- Offer, Contract, Policy, etc.
    template_content LONGTEXT NOT NULL,  -- HTML/JSON template
    file_extension VARCHAR(10),  -- .docx, .pdf, .html
    is_active BOOL DEFAULT TRUE,
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6),
    deleted_at DATETIME(6) NULL,
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    INDEX ix_document_templates_company_id (company_id),
    INDEX ix_document_templates_category (company_id, category)
);
```
**Why:** Store offer letter, employment contract, policy document templates

---

#### 2. **compliance_checklist** (for HR compliance tracking)
```sql
CREATE TABLE compliance_checklist (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    name VARCHAR(200) NOT NULL,  -- "Employee Onboarding", "GDPR", "Tax Compliance"
    description TEXT,
    items JSON NOT NULL,  -- Array of checklist items
    frequency VARCHAR(20),  -- Monthly, Quarterly, Annually
    due_date DATE,
    is_active BOOL DEFAULT TRUE,
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    deleted_at DATETIME(6) NULL,
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    INDEX ix_compliance_checklist_company_id (company_id),
    UNIQUE KEY uk_compliance_checklist_company (company_id, name)
);
```
**Why:** Track compliance requirements (GDPR, tax, labor laws)

---

#### 3. **compliance_evidence** (for compliance documentation)
```sql
CREATE TABLE compliance_evidence (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    checklist_id INT NOT NULL,
    item_id INT,  -- Index within checklist.items JSON
    status VARCHAR(30),  -- Pending, Completed, Failed
    evidence_document_path VARCHAR(500),
    verified_by_user_id VARCHAR(50),
    verified_at DATETIME(6),
    comments TEXT,
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    FOREIGN KEY (checklist_id) REFERENCES compliance_checklist(id) ON DELETE CASCADE,
    INDEX ix_compliance_evidence_company_id (company_id),
    INDEX ix_compliance_evidence_checklist_id (checklist_id),
    INDEX ix_compliance_evidence_status (company_id, status)
);
```
**Why:** Track evidence for completed compliance items

---

#### 4. **employee_skills** (for skill inventory)
```sql
CREATE TABLE employee_skills (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    employee_id INT NOT NULL,
    skill_name VARCHAR(200) NOT NULL,
    proficiency_level VARCHAR(20),  -- Beginner, Intermediate, Expert
    years_of_experience DECIMAL(4,1),
    verified BOOL DEFAULT FALSE,
    verified_by_user_id VARCHAR(50),
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6),
    deleted_at DATETIME(6) NULL,
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    INDEX ix_employee_skills_employee_id (employee_id),
    INDEX ix_employee_skills_skill_name (company_id, skill_name)
);
```
**Why:** Maintain employee skill matrix for project allocation

---

#### 5. **project_assignments** (for project management)
```sql
CREATE TABLE project_assignments (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    project_name VARCHAR(200) NOT NULL,
    project_code VARCHAR(50) UNIQUE,
    employee_id INT NOT NULL,
    role VARCHAR(100),  -- Developer, Manager, Tester
    allocation_percentage INT,  -- 0-100
    start_date DATE NOT NULL,
    end_date DATE,
    status VARCHAR(30),  -- Assigned, InProgress, Completed, OnHold
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6),
    deleted_at DATETIME(6) NULL,
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    INDEX ix_project_assignments_company_id (company_id),
    INDEX ix_project_assignments_employee_id (employee_id),
    INDEX ix_project_assignments_status (company_id, status)
);
```
**Why:** Track employee project assignments & allocation

---

#### 6. **expense_policies** (for expense management)
```sql
CREATE TABLE expense_policies (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    policy_name VARCHAR(200) NOT NULL,
    description TEXT,
    category VARCHAR(50),  -- Travel, Meals, Transport, etc.
    max_amount_per_transaction DECIMAL(14,2),
    max_amount_per_month DECIMAL(14,2),
    requires_approval BOOL DEFAULT TRUE,
    approver_level INT,  -- 1=Manager, 2=Director, 3=Finance
    is_active BOOL DEFAULT TRUE,
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    deleted_at DATETIME(6) NULL,
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    INDEX ix_expense_policies_company_id (company_id),
    UNIQUE KEY uk_expense_policies_company (company_id, category)
);
```
**Why:** Define company expense policies and limits

---

### MEDIUM PRIORITY (Nice to Have)

#### 7. **bank_account_details** (for payroll)
```sql
CREATE TABLE bank_account_details (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    employee_id INT NOT NULL,
    account_holder_name VARCHAR(200) NOT NULL,
    account_number VARCHAR(50) NOT NULL,
    ifsc_code VARCHAR(20) NOT NULL,
    account_type VARCHAR(20),  -- Salary, Personal, Joint
    is_primary BOOL DEFAULT TRUE,
    is_verified BOOL DEFAULT FALSE,
    verified_at DATETIME(6),
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6),
    deleted_at DATETIME(6) NULL,
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    UNIQUE KEY uk_bank_account_employee (employee_id, account_number),
    INDEX ix_bank_account_details_company_id (company_id),
    INDEX ix_bank_account_details_is_primary (employee_id, is_primary)
);
```
**Why:** Store multiple bank accounts per employee

---

#### 8. **emergency_contacts** (employee emergency info)
```sql
CREATE TABLE emergency_contacts (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    employee_id INT NOT NULL,
    contact_name VARCHAR(200) NOT NULL,
    relationship VARCHAR(50),  -- Spouse, Parent, Sibling, Friend
    phone VARCHAR(20) NOT NULL,
    email VARCHAR(200),
    address TEXT,
    priority INT DEFAULT 1,  -- 1=Primary, 2=Secondary, 3=Tertiary
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6),
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    INDEX ix_emergency_contacts_employee_id (employee_id),
    INDEX ix_emergency_contacts_priority (employee_id, priority)
);
```
**Why:** Store employee emergency contact information

---

#### 9. **salary_structure_component** (payroll components)
```sql
CREATE TABLE salary_structure_component (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    salary_structure_id INT NOT NULL,
    component_type VARCHAR(50),  -- Basic, HRA, DA, Bonus, Tax, etc.
    component_name VARCHAR(200) NOT NULL,
    component_value DECIMAL(14,2),
    value_type VARCHAR(20),  -- Fixed, Percentage, Formula
    formula_expression VARCHAR(500),  -- e.g., "BasicSalary * 0.5"
    is_active BOOL DEFAULT TRUE,
    sequence INT,  -- For ordering
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    FOREIGN KEY (salary_structure_id) REFERENCES salary_structures(id) ON DELETE CASCADE,
    INDEX ix_salary_structure_component_salary_structure_id (salary_structure_id)
);
```
**Why:** Break down salary structure into components

---

#### 10. **award_recognition** (for employee recognition)
```sql
CREATE TABLE award_recognition (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT NOT NULL,
    employee_id INT NOT NULL,
    award_name VARCHAR(200) NOT NULL,
    award_type VARCHAR(50),  -- Performance, Innovation, Attendance, Culture
    award_date DATE NOT NULL,
    awarded_by_user_id VARCHAR(50),
    prize_amount DECIMAL(14,2),
    certificate_path VARCHAR(500),
    description TEXT,
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    deleted_at DATETIME(6) NULL,
    
    FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    INDEX ix_award_recognition_employee_id (employee_id),
    INDEX ix_award_recognition_award_type (company_id, award_type),
    INDEX ix_award_recognition_award_date (award_date DESC)
);
```
**Why:** Track employee awards and recognition

---

### LOW PRIORITY (Future Enhancement)

#### 11. **api_audit_log** (API request tracking)
```sql
CREATE TABLE api_audit_log (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    company_id INT,
    user_id VARCHAR(50),
    endpoint VARCHAR(500) NOT NULL,
    method VARCHAR(10),  -- GET, POST, PUT, DELETE
    status_code INT,
    request_body LONGTEXT,
    response_body LONGTEXT,
    ip_address VARCHAR(50),
    user_agent VARCHAR(500),
    duration_ms INT,
    occurred_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    
    INDEX ix_api_audit_log_company_id (company_id),
    INDEX ix_api_audit_log_user_id (user_id),
    INDEX ix_api_audit_log_occurred_at (occurred_at DESC),
    INDEX ix_api_audit_log_status_code (status_code)
);
```
**Why:** Comprehensive API request logging for debugging

---

#### 12. **system_settings** (global system configuration)
```sql
CREATE TABLE system_settings (
    id INT PRIMARY KEY AUTO_INCREMENT,
    company_id INT,  -- NULL for global settings
    setting_key VARCHAR(200) UNIQUE NOT NULL,  -- e.g., "default_currency", "timezone"
    setting_value LONGTEXT,
    setting_type VARCHAR(50),  -- String, Int, Boolean, Json
    description TEXT,
    is_encrypted BOOL DEFAULT FALSE,
    created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6),
    
    INDEX ix_system_settings_company_id (company_id),
    INDEX ix_system_settings_setting_key (setting_key)
);
```
**Why:** Store system and company-level configuration

---

## 📊 Summary

### Fixes Applied: 2 Migrations
```
✅ 20260812093000_AddPiiEncryptionColumns
   - Modified: 2 tables (employees, sales_customers)
   - Added: 12 columns + 2 indexes
   - Purpose: Encrypt PII data (GDPR compliance)

✅ 20260812094000_AddSoftDeletesForSalesEntities
   - Modified: 10 tables
   - Added: 10 columns (deleted_at) + 10 indexes
   - Purpose: Support soft deletes (data privacy)
```

### Recommended New Tables: 12

| Priority | Table | Purpose |
|----------|-------|---------|
| HIGH | document_templates | Store HR document templates |
| HIGH | compliance_checklist | Track compliance requirements |
| HIGH | compliance_evidence | Track compliance verification |
| HIGH | employee_skills | Maintain skill inventory |
| HIGH | project_assignments | Track project allocations |
| HIGH | expense_policies | Define expense limits & rules |
| MEDIUM | bank_account_details | Multiple bank accounts |
| MEDIUM | emergency_contacts | Emergency contact info |
| MEDIUM | salary_structure_component | Salary breakdown |
| MEDIUM | award_recognition | Employee awards & recognition |
| LOW | api_audit_log | API request tracking |
| LOW | system_settings | System configuration |

---

## 🎯 Next Steps

### Phase 1: Deploy Current Fixes ✅ READY
```bash
dotnet ef database update
# Applies both migrations automatically
```

### Phase 2: Add High-Priority Tables (Optional)
```bash
# Create new migration for recommended tables
dotnet ef migrations add AddMissingTables \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API
```

### Phase 3: Update DbContext (If Adding Tables)
```csharp
// In ApplicationDbContext.cs
public DbSet<DocumentTemplate> DocumentTemplates { get; set; }
public DbSet<ComplianceChecklist> ComplianceChecklists { get; set; }
public DbSet<ComplianceEvidence> ComplianceEvidences { get; set; }
// ... etc
```

---

**Note:** All new tables should include:
- CompanyId (for multi-tenancy)
- CreatedAt/UpdatedAt (audit trail)
- DeletedAt (soft delete support)
- Appropriate indexes for common queries
