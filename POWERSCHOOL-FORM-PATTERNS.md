# PowerSchool Database Extension Form Patterns

## Overview

This document describes the **universal patterns** for PowerSchool HTML forms that handle INSERT/UPDATE/DELETE operations for **any database extension** (custom tables). These patterns apply to all standalone and child tables defined in PowerSchool extensions.

---

## Universal Form Naming Pattern

All PowerSchool extension form fields follow this structure:

```
CF-[CONTEXT]FIELDNAME[FORMAT]
```

**Components**:
- `CF-` = Custom Field prefix (required)
- `[CONTEXT]` = Table identifier and record reference
- `FIELDNAME` = Database column name (uppercase)
- `[FORMAT]` = Optional formatting suffix (e.g., `$format=date`, `$formatnumeric=...`)

---

## Context Patterns by Table Type

### Standalone Tables

**Format**:
```
CF-[:SCOPE.EXTENSION_NAME.TABLE_NAME:RECORD_ID]FIELDNAME
```

**Template**:
```
CF-[:0.{ExtensionName}.{TABLE_NAME}:{RecordID}]FIELDNAME
```

**Examples**:
- New record: `CF-[:0.U_UD_UserData.U_UD_CATEGORY:-1]NAME`
- Update record: `CF-[:0.U_UD_UserData.U_UD_CATEGORY:917850]NAME`
- Another extension: `CF-[:0.MyCustomExt.MY_LOOKUP_TABLE:12345]DESCRIPTION`

### Child Tables (Extending Core or Standalone Tables)

**Format**:
```
CF-[PARENT_TABLE:PARENT_ID.EXTENSION_NAME.CHILD_TABLE:CHILD_ID]FIELDNAME
```

**Template**:
```
CF-[{ParentTable}:{ParentRecordID}.{ExtensionName}.{CHILD_TABLE}:{ChildRecordID}]FIELDNAME
```

**Examples**:
- New child: `CF-[U_UD_RECORD:917865.U_UD_UserData.U_UD_VALUE:-1]VALUE_STR`
- Update child: `CF-[U_UD_RECORD:917865.U_UD_UserData.U_UD_VALUE:123456]VALUE_STR`
- Core table child: `CF-[STUDENTS:3302.MyExtension.STUDENT_CUSTOM:-1]CUSTOM_FIELD`

**Key Differences**:
- Standalone: Uses `:0` scope prefix
- Child: Uses `PARENT_TABLE:PARENT_ID` prefix (establishes foreign key relationship)

---

## Example: U_UD_UserData Extension

### Schema Hierarchy
```
u_ud_category (Standalone: Categories)
    ↓ referenced by
u_ud_fielddef (Standalone: Field definitions)
    ↓ defines fields for
u_ud_record (Standalone: Record header)
    ↓ has children
u_ud_value (Child of u_ud_record: Values)
```

---

## Universal INSERT/UPDATE/DELETE Operations

### Operation Detection by Record ID

PowerSchool determines the operation type based on the **Record ID** in the field name:

| Record ID Pattern | Operation | Description |
|-------------------|-----------|-------------|
| Negative integer (`-1`, `-2`, `-N`) | **INSERT** | Create new record with auto-generated ID |
| Special placeholder (`-{INSERT_COUNTER}`) | **INSERT** | Template for dynamic row generation |
| Positive integer (`917850`, `123456`) | **UPDATE** | Modify existing record by ID |
| Row omitted from form | **DELETE** | Remove record (if previously existed) |
| Delete checkbox checked | **DELETE** | Explicit deletion flag |

---

## INSERT Pattern (New Records)

### Standalone Table Insert

**Pattern**:
```html
<input name="CF-[:0.{ExtensionName}.{TABLE_NAME}:-{N}]FIELDNAME" value="..." />
```

**Example** (New category):
```html
<input type="text" 
  name="CF-[:0.U_UD_UserData.U_UD_CATEGORY:-1]NAME" 
  value="New Category" />
  
<input type="text" 
  name="CF-[:0.U_UD_UserData.U_UD_CATEGORY:-1]CODE" 
  value="new_code" />
```

**How PowerSchool Processes**:
1. Detects `-1` as new record indicator
2. Inserts row into `u_ud_category` table
3. Returns auto-generated ID (e.g., 917851)
4. Sets audit fields: `created_on`, `created_by`

### Child Table Insert

**Pattern**:
```html
<input name="CF-[{ParentTable}:{ParentID}.{Extension}.{CHILD_TABLE}:-{N}]FIELDNAME" value="..." />
```

**Example** (New value under existing record):
```html
<input type="text" 
  name="CF-[U_UD_RECORD:917865.U_UD_UserData.U_UD_VALUE:-1]FIELDDEF_ID" 
  value="201" />
  
<input type="text" 
  name="CF-[U_UD_RECORD:917865.U_UD_UserData.U_UD_VALUE:-1]VALUE_NUM" 
  value="85.5" />
```

**How PowerSchool Processes**:
1. Detects `-1` as new record indicator
2. Extracts parent context: `U_UD_RECORD:917865`
3. Inserts row into `u_ud_value` table
4. **Automatically sets** `record_id=917865` (foreign key from parent context)
5. Returns auto-generated ID for the new value row

### Multiple New Records (Counter Pattern)

For **dynamic forms** that allow adding multiple rows:

```html
<!-- Row 1 -->
<input name="CF-[:0.MyExtension.MY_TABLE:-1]FIELD_A" value="..." />
<input name="CF-[:0.MyExtension.MY_TABLE:-1]FIELD_B" value="..." />

<!-- Row 2 -->
<input name="CF-[:0.MyExtension.MY_TABLE:-2]FIELD_A" value="..." />
<input name="CF-[:0.MyExtension.MY_TABLE:-2]FIELD_B" value="..." />

<!-- Row 3 -->
<input name="CF-[:0.MyExtension.MY_TABLE:-3]FIELD_A" value="..." />
<input name="CF-[:0.MyExtension.MY_TABLE:-3]FIELD_B" value="..." />
```

**Rules**:
- Same counter groups fields for same record (e.g., all `-1` fields = one row)
- Counters can be any negative integer
- PowerSchool groups by counter and inserts one record per unique counter

---

## UPDATE Pattern (Existing Records)

### Standalone Table Update

**Pattern**:
```html
<input name="CF-[:0.{ExtensionName}.{TABLE_NAME}:{RecordID}]FIELDNAME" value="..." />
```

**Example** (Update existing category):
```html
<input type="text" 
  name="CF-[:0.U_UD_UserData.U_UD_CATEGORY:917850]NAME" 
  value="Updated Name" />
  
<input type="checkbox" 
  name="CF-[:0.U_UD_UserData.U_UD_CATEGORY:917850]IS_ACTIVE" 
  value="1" 
  checked />
```

**How PowerSchool Processes**:
1. Detects `917850` as existing record ID
2. Updates `u_ud_category` WHERE `id=917850`
3. Updates only submitted fields
4. Sets audit fields: `updated_on`, `updated_by`

### Child Table Update

**Pattern**:
```html
<input name="CF-[{ParentTable}:{ParentID}.{Extension}.{CHILD_TABLE}:{ChildID}]FIELDNAME" value="..." />
```

**Example** (Update existing value):
```html
<input type="text" 
  name="CF-[U_UD_RECORD:917865.U_UD_UserData.U_UD_VALUE:123456]VALUE_NUM" 
  value="92.3" />
```

**How PowerSchool Processes**:
1. Detects `123456` as existing child record ID
2. Updates `u_ud_value` WHERE `id=123456`
3. Validates parent relationship still matches
4. Updates audit fields

---

## DELETE Pattern (Remove Records)

### Method 1: Delete Column Checkbox

**Pattern**:
```html
<tr>
  <td>
    <input name="CF-[:0.{Extension}.{TABLE}:{RecordID}]FIELD1" value="..." />
  </td>
  <td class="deleteCol">
    <input type="checkbox" name="deleterow" value="{RecordID}" />
  </td>
</tr>
```

**How PowerSchool Processes**:
- Checks for `deleterow` checkboxes with values
- Deletes records matching checked IDs
- Respects foreign key constraints (may cascade or prevent delete)

### Method 2: Row Omission (Implicit Delete)

**Pattern**: Simply don't include the row in form submission

**Original Form** (Loaded from DB):
```html
<input name="CF-[:0.MyExt.MY_TABLE:100]FIELD_A" value="..." />
<input name="CF-[:0.MyExt.MY_TABLE:101]FIELD_A" value="..." />
<input name="CF-[:0.MyExt.MY_TABLE:102]FIELD_A" value="..." />
```

**Submitted Form** (User removed row 101):
```html
<input name="CF-[:0.MyExt.MY_TABLE:100]FIELD_A" value="..." />
<!-- Row 101 omitted -->
<input name="CF-[:0.MyExt.MY_TABLE:102]FIELD_A" value="..." />
```

**How PowerSchool Processes**:
1. Loads original record IDs from database
2. Compares with submitted record IDs
3. Missing IDs are deleted from table

**⚠️ Note**: This method depends on form implementation. Some forms use explicit delete checkboxes instead.

### Method 3: Soft Delete (Status Flag)

Some tables use status/active flags instead of physical deletion:

```html
<input type="hidden" name="CF-[:0.MyExt.MY_TABLE:100]IS_ACTIVE" value="" />
<input type="checkbox" name="CF-[:0.MyExt.MY_TABLE:100]IS_ACTIVE" value="1" />
<!-- Unchecked = soft delete -->
```

---

## Special Field Patterns

### Boolean Fields (Checkboxes)

**Always use hidden input + checkbox pattern**:

```html
<!-- Hidden ensures unchecked = false -->
<input type="hidden" name="CF-[CONTEXT]FIELD_NAME" value="" />

<!-- Checkbox overrides hidden when checked -->
<input type="checkbox" name="CF-[CONTEXT]FIELD_NAME" value="1" checked />
```

**Why?**: Unchecked checkboxes don't submit values. Hidden input provides default `false` state.

**Processing**:
- Unchecked: Hidden value `""` submitted → false
- Checked: Checkbox value `"1"` overrides hidden → true

### Date Fields

**Pattern**:
```html
<input type="text" 
  class="psDateWidget" 
  name="CF-[CONTEXT]DATE_FIELD$format=date" 
  value="01/29/2026" 
  data-validation='{"type":"date","key":"table.date_field"}' />
```

**Key Points**:
- `$format=date` suffix tells PowerSchool to parse as date
- `psDateWidget` class triggers date picker UI
- Validation ensures proper date format

### Numeric Fields

**Pattern**:
```html
<input type="text" 
  class="psNumWidget" 
  name="CF-[CONTEXT]AMOUNT$formatnumeric=#########.#####" 
  value="1234.56" 
  data-validation='{"type":"number","minValue":"0","maxValue":"99999.99"}' />
```

**Key Points**:
- `$formatnumeric=...` defines display format
- `psNumWidget` class for numeric input handling
- Validation enforces min/max constraints

### Text Areas (CLOB fields)

**Pattern**:
```html
<textarea 
  name="CF-[CONTEXT]NOTES" 
  rows="5" 
  cols="50">Long text content here...</textarea>
```

**Key Points**:
- No special formatting suffix needed
- Handles unlimited text (CLOB/TEXT database type)
- No `maxlength` attribute

---

## Form Submission Endpoint

**Standard POST Target**:
```html
<form action="/admin/changesrecorded.white.html" method="POST">
  <!-- Fields here -->
  <input type="hidden" name="ac" value="prim" />
  <button type="submit">Submit</button>
</form>
```

**Required Parameters**:
- `ac=prim` - Action parameter for PowerSchool's form processor
- `method="POST"` - Always POST, never GET

---

## Processing Logic Summary

### Backend Processing Steps

1. **Parse field names** to extract:
   - Extension name
   - Table name
   - Record ID (or counter)
   - Parent context (if child table)
   - Field name

2. **Group fields by record**:
   - Same Record ID → same database row
   - Same counter (negative) → new row being inserted

3. **Determine operation per record**:
   - Negative ID/counter → INSERT
   - Positive ID → UPDATE
   - Missing from form → DELETE (if tracking original IDs)

4. **Execute database operations**:
   - INSERT: Generate new ID, set created audit fields
   - UPDATE: Modify existing row, set updated audit fields
   - DELETE: Remove row or set inactive flag

5. **Handle relationships**:
   - For child tables, extract and set foreign key from parent context
   - Validate foreign key constraints
   - Cascade deletes if configured

6. **Validate constraints**:
   - Check data types match field definitions
   - Enforce min/max values, string lengths
   - Validate required fields are present

---

## Example: Complete CRUD Form

### Scenario: Custom Extension for Student Awards

**Extension**: `StudentAwards`
**Tables**:
- `AWARD_TYPE` (standalone) - Award categories
- `STUDENT_AWARD` (child of core `STUDENTS`) - Awards per student

### Award Type Management (Standalone)

```html
<form action="/admin/changesrecorded.white.html" method="POST">
  
  <!-- Update Existing Award Type -->
  <tr>
    <td>
      <input type="text" 
        name="CF-[:0.StudentAwards.AWARD_TYPE:501]AWARD_NAME" 
        value="Honor Roll" />
    </td>
    <td>
      <input type="hidden" name="CF-[:0.StudentAwards.AWARD_TYPE:501]IS_ACTIVE" value="" />
      <input type="checkbox" 
        name="CF-[:0.StudentAwards.AWARD_TYPE:501]IS_ACTIVE" 
        value="1" 
        checked />
    </td>
  </tr>
  
  <!-- Insert New Award Type -->
  <tr>
    <td>
      <input type="text" 
        name="CF-[:0.StudentAwards.AWARD_TYPE:-1]AWARD_NAME" 
        value="Perfect Attendance" />
    </td>
    <td>
      <input type="hidden" name="CF-[:0.StudentAwards.AWARD_TYPE:-1]IS_ACTIVE" value="" />
      <input type="checkbox" 
        name="CF-[:0.StudentAwards.AWARD_TYPE:-1]IS_ACTIVE" 
        value="1" 
        checked />
    </td>
  </tr>
  
  <input type="hidden" name="ac" value="prim" />
  <button type="submit">Save Award Types</button>
</form>
```

### Student Award Assignment (Child Table)

```html
<form action="/admin/changesrecorded.white.html" method="POST">
  
  <!-- Update Existing Student Award (Student ID: 3302) -->
  <tr>
    <td>
      <input type="text" 
        name="CF-[STUDENTS:3302.StudentAwards.STUDENT_AWARD:9001]AWARD_TYPE_ID" 
        value="501" />
    </td>
    <td>
      <input type="text" 
        name="CF-[STUDENTS:3302.StudentAwards.STUDENT_AWARD:9001]AWARD_DATE$format=date" 
        value="01/15/2026" />
    </td>
  </tr>
  
  <!-- Insert New Award for Same Student -->
  <tr>
    <td>
      <input type="text" 
        name="CF-[STUDENTS:3302.StudentAwards.STUDENT_AWARD:-1]AWARD_TYPE_ID" 
        value="502" />
    </td>
    <td>
      <input type="text" 
        name="CF-[STUDENTS:3302.StudentAwards.STUDENT_AWARD:-1]AWARD_DATE$format=date" 
        value="01/29/2026" />
    </td>
  </tr>
  
  <input type="hidden" name="ac" value="prim" />
  <button type="submit">Save Student Awards</button>
</form>
```

**Result**:
- Updates `STUDENT_AWARD` ID 9001
- Inserts new `STUDENT_AWARD` with auto-generated ID
- PowerSchool automatically sets `studentsdcid=3302` from parent context `STUDENTS:3302`

---

## Validation Attributes

PowerSchool forms include JSON validation in `data-validation` or `data-validation-add` attributes:

```json
{
  "type": "text|number|date|boolean",
  "key": "table_name.field_name",
  "maxlength": "200",
  "minValue": "-999999.99",
  "maxValue": "999999.99",
  "isinteger": "true",
  "required": true
}
```

**Validation Timing**:
- **Client-side**: JavaScript before form submission
- **Server-side**: PowerSchool backend before database write

---

## U_UD_UserData Example Tables

### Table Structures Reference

### 1. U_UD_CATEGORY Table

**Purpose**: Master list of user-defined data categories (e.g. MAP Scores, Club Experience, Skillsets)

**Relationships**:
- **Children**: u_ud_fielddef entries (field definitions per category)
- **Children**: u_ud_record entries (one record per user per category)

**Core Fields**:
| Field | Type | Length | Purpose |
|-------|------|--------|---------|
| name | String | 200 | Display name of the category |
| code | String | 60 | Unique code/identifier for the category |
| is_active | Boolean | - | Active/inactive flag |
| sort_order | Integer | - | Display order in lists |

**Audit Fields**: created_on, created_by, updated_on, updated_by (String max 100)

**Indexes**:
- `u_ud_category_code_idx` on `code` - lookup category by code

**Form Example**:
```html
<td id="NAME_917850" class="td-NAME">
  <input type="text" class="NAME psTextWidget" 
    value="&nbsp;cname1" 
    maxlength="200" 
    data-validation='{"maxlength":"200","type":"text","key":"u_ud_category.name"}' 
    name="CF-[:0.U_UD_UserData.U_UD_CATEGORY:917850]NAME" />
</td>
```

---

### 2. U_UD_FIELDDEF Table

**Purpose**: Field definitions/metadata for user-defined data categories (defines what fields exist in each category)

**Relationships**:
- **Parent**: u_ud_category (via `category_id`) - the category this field belongs to
- **Children**: u_ud_value entries (one value per record per field)

**Core Fields**:
| Field | Type | Length | Purpose |
|-------|------|--------|---------|
| category_id | Integer | - | Parent category ID (indexed, part of composite key) |
| field_key | String | 100 | Unique field identifier within category (part of composite key) |
| field_label | String | 200 | Display label for the field |
| value_type | String | 20 | Data type: "string", "numeric", "date", "boolean", "text" |
| required | Boolean | - | Whether field is mandatory |
| sort_order | Integer | - | Display order within category |
| help_text | String | 1000 | Help/tooltip text for users |

**Audit Fields**: created_on, created_by, updated_on, updated_by (String max 100)

**Indexes**:
- `u_ud_fielddef_cat_idx` on `category_id` - find all fields in a category
- `u_ud_fielddef_cat_key_idx` on `(category_id, field_key)` - ensures unique field keys per category

**Form Example** (Boolean Field with Hidden Input):
```html
<td id="REQUIRED_917853" class="td-REQUIRED">
  <input type="hidden" name="CF-[:0.U_UD_UserData.U_UD_FIELDDEF:917853]REQUIRED" value="">
  <input type="checkbox" class="REQUIRED" 
    data-validation='{"type":"boolean","key":"u_ud_fielddef.required"}' 
    name="CF-[:0.U_UD_UserData.U_UD_FIELDDEF:917853]REQUIRED" 
    value="1" />
</td>
```

---

### 3. U_UD_RECORD Table

**Purpose**: Header record for user-defined data collection per user+category combination

**Relationships**:
- **Reference**: u_ud_category (via `category_id`)
- **Reference**: users table (via `usersdcid`) - PowerSchool users
- **Children**: u_ud_value entries (multiple field values per record)

**Core Fields**:
| Field | Type | Purpose |
|-------|------|---------|
| usersdcid | Integer | PowerSchool user ID (indexed, part of composite key) |
| category_id | Integer | User Data category ID (indexed, part of composite key) |

**Audit Fields**: created_on, created_by, updated_on, updated_by (String max 100)

**Indexes**:
- `u_ud_record_users_cat_idx` on `(usersdcid, category_id)` - ensures one record per user per category
- `u_ud_record_cat_idx` on `category_id` - find all records in a category

**Composite Key Pattern**: (usersdcid, category_id) - prevents duplicate user+category records

**Form Example**:
```html
<td id="USERSDCID_917863" class="td-USERSDCID">
  <input type="text" class="USERSDCID psNumWidget" 
    value="3302" 
    data-validation='{"minValue":"-2147483648","maxValue":"2147483647","isinteger":"true","type":"number","key":"u_ud_record.usersdcid"}' 
    name="CF-[:0.U_UD_UserData.U_UD_RECORD:917863]USERSDCID$formatnumeric=#########.#####" />
</td>
```

---

### 4. U_UD_VALUE Table

**Purpose**: Stores user-defined field values with multi-type support (string, numeric, date, boolean, text)

**Relationships**:
- **Parent**: u_ud_record (via `record_id`) - the record header for a user+category combination
- **Reference**: u_ud_fielddef (via `fielddef_id`) - the field definition/metadata

**Core Fields**:
| Field | Type | Length | Purpose |
|-------|------|--------|---------|
| record_id | Integer | - | Foreign key to u_ud_record (indexed) |
| fielddef_id | Integer | - | Foreign key to u_ud_fielddef (indexed) |
| value_str | String | 4000 | String value storage |
| value_num | Double | - | Numeric value storage |
| value_date | Date | - | Date value storage |
| value_bool | Boolean | - | Boolean/checkbox value storage |
| value_text | Clob | - | Large text value storage (unlimited) |

**Audit Fields**: created_on, created_by, updated_on, updated_by (String max 100)

**Indexes**:
- `u_ud_value_record_idx` on `record_id`
- `u_ud_value_record_field_idx` on `(record_id, fielddef_id)` - prevents duplicate field values per record

**Key Pattern**: One row per field per record (no duplicate fielddef_id entries in same record)

**Form Example** (New Record with Parent-Child Syntax):
```html
<tr class="new">
  <td id="RECORD_ID_-{INSERT_COUNTER}" class="td-RECORD_ID">
    <input type="text" class="RECORD_ID" 
      data-addclass="psNumWidget" 
      value="" 
      data-validation-add='{"minValue":"-2147483648","maxValue":"2147483647","isinteger":"true","type":"number","key":"u_ud_record.u_ud_value.record_id"}' 
      data-name="CF-[U_UD_RECORD:917865.U_UD_UserData.U_UD_VALUE:-{INSERT_COUNTER}]RECORD_ID$formatnumeric=#########.#####" />
  </td>
  <!-- Additional fields... -->
</tr>
```

---

## Implementation Checklist

### For Custom Extension Developers

#### Creating Forms

- [ ] Use correct context pattern for table type (standalone vs child)
- [ ] Use negative IDs (`-1`, `-2`, etc.) for new record rows
- [ ] Use actual record IDs for updating existing rows
- [ ] Include hidden input before checkboxes for boolean fields
- [ ] Add `$format=date` suffix for date fields
- [ ] Add `$formatnumeric=...` for numeric formatting
- [ ] Include `data-validation` attributes for client-side validation
- [ ] Add proper CSS classes (`psDateWidget`, `psNumWidget`, `psTextWidget`)
- [ ] Include `ac=prim` hidden parameter
- [ ] POST to `/admin/changesrecorded.white.html`

#### Processing Submissions

- [ ] Parse `CF-[CONTEXT]FIELDNAME` pattern to extract table/record info
- [ ] Group fields by record ID/counter
- [ ] Detect operation type from record ID (negative=INSERT, positive=UPDATE)
- [ ] For child tables, extract parent ID and set foreign key
- [ ] Execute database operations in correct order (parents before children)
- [ ] Set audit fields automatically (created_on, created_by, updated_on, updated_by)
- [ ] Handle boolean fields (empty string = false, "1" = true)
- [ ] Apply server-side validation before database write
- [ ] Return error messages for constraint violations

### For Form Builders

#### Dynamic Row Templates

Use placeholder that JavaScript can replace:

```html
<tr class="template" style="display:none;">
  <td>
    <input name="CF-[:0.MyExt.MY_TABLE:-{INSERT_COUNTER}]FIELD_A" />
  </td>
  <td>
    <input name="CF-[:0.MyExt.MY_TABLE:-{INSERT_COUNTER}]FIELD_B" />
  </td>
</tr>

<script>
// When user clicks "Add Row", clone template and replace {INSERT_COUNTER}
let counter = 1;
function addRow() {
  let template = document.querySelector('.template');
  let clone = template.cloneNode(true);
  clone.style.display = '';
  clone.innerHTML = clone.innerHTML.replace(/-\{INSERT_COUNTER\}/g, '-' + counter);
  counter++;
  document.querySelector('tbody').appendChild(clone);
}
</script>
```

---

## Common Pitfalls & Solutions

### Problem: Checkbox always stays checked
**Cause**: Missing hidden input before checkbox
**Solution**: Always include hidden input with empty value

### Problem: Child record not linked to parent
**Cause**: Incorrect parent context syntax
**Solution**: Use `CF-[PARENT_TABLE:PARENT_ID.Extension.CHILD_TABLE:-1]...`

### Problem: Date not saving correctly
**Cause**: Missing `$format=date` suffix
**Solution**: Add format suffix: `FIELDNAME$format=date`

### Problem: New records not inserting
**Cause**: Using `0` or positive numbers instead of negative
**Solution**: Use negative integers: `-1`, `-2`, `-3`, etc.

### Problem: Updates creating duplicates
**Cause**: Using negative IDs for existing records
**Solution**: Use actual positive record IDs for updates

### Problem: Decimal values truncated
**Cause**: Missing numeric formatting
**Solution**: Add `$formatnumeric=...` suffix or ensure database column is numeric type

---

## Advanced Patterns

### Conditional Fields Based on Type

```html
<!-- User selects value type -->
<select name="CF-[:0.MyExt.MY_TABLE:-1]VALUE_TYPE">
  <option value="string">String</option>
  <option value="number">Number</option>
  <option value="date">Date</option>
</select>

<!-- Show/hide appropriate input based on selection -->
<input type="text" 
  class="value-string" 
  name="CF-[:0.MyExt.MY_TABLE:-1]VALUE_STR" />

<input type="text" 
  class="value-number psNumWidget" 
  name="CF-[:0.MyExt.MY_TABLE:-1]VALUE_NUM$formatnumeric=#.##" />

<input type="text" 
  class="value-date psDateWidget" 
  name="CF-[:0.MyExt.MY_TABLE:-1]VALUE_DATE$format=date" />
```

### Multi-Level Parent-Child Relationships

```html
<!-- Grandparent → Parent → Child -->
<input name="CF-[STUDENTS:3302.MyExt.PARENT_TABLE:100.MyExt.CHILD_TABLE:-1]FIELD" />
```

**Note**: Verify PowerSchool supports multi-level nesting in your version.

---

## Quick Reference Templates

### Standalone Table - New Record
```html
<input name="CF-[:0.{ExtensionName}.{TABLE_NAME}:-1]FIELD_NAME" value="..." />
```

### Standalone Table - Update Record
```html
<input name="CF-[:0.{ExtensionName}.{TABLE_NAME}:{RecordID}]FIELD_NAME" value="..." />
```

### Child Table - New Record
```html
<input name="CF-[{ParentTable}:{ParentID}.{ExtensionName}.{CHILD_TABLE}:-1]FIELD_NAME" value="..." />
```

### Child Table - Update Record
```html
<input name="CF-[{ParentTable}:{ParentID}.{ExtensionName}.{CHILD_TABLE}:{ChildID}]FIELD_NAME" value="..." />
```

### Boolean Field
```html
<input type="hidden" name="CF-[CONTEXT]BOOL_FIELD" value="" />
<input type="checkbox" name="CF-[CONTEXT]BOOL_FIELD" value="1" />
```

### Date Field
```html
<input type="text" class="psDateWidget" name="CF-[CONTEXT]DATE_FIELD$format=date" />
```

### Numeric Field
```html
<input type="text" class="psNumWidget" name="CF-[CONTEXT]NUM_FIELD$formatnumeric=#.##" />
```

### Text Area
```html
<textarea name="CF-[CONTEXT]TEXT_FIELD"></textarea>
```

### Form Submit
```html
<form action="/admin/changesrecorded.white.html" method="POST">
  <!-- Fields -->
  <input type="hidden" name="ac" value="prim" />
  <button type="submit">Submit</button>
</form>
```

---

## Related Documentation

- PowerSchool Extension Schema: `psExtension.xsd`
- Custom Field Documentation: PowerSchool Developer Guide
- Example Extension: [U_UD_UserData.xml](powerschool-plugin/FileServiceTools/user_schema_root/U_UD_UserData.xml)

---

## Version History

- **v1.0** - Initial documentation with U_UD_UserData example
- **v2.0** - Generalized patterns for all database extensions
