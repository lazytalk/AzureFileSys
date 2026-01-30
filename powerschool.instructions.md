# Developer Admin

Toggle navigation [PowerSource](/)

# Page Customization and Database Extensions

## One-To-One Table Extensions

To work with a one-to-one table extension on a page in PowerSchool, reference the primary table, the database extension group name, and the field name in the following format:

\[PrimaryTable.ExtensionGroupName\]Field\_Name

The examples below show a variety of ways to reference a one-to-one extension to the Students table. For this example we have added a table extension to track loaned laptops. The extension group name in these examples is U\_Laptop.

### Sample Code:  Various Form Elements Using One-To-One Extensions

**<!--  Entry Field -->**
<tr>
     <td class="bold">Model Number</td>
     <td>
     <input type="text" name="\[Students.U\_Laptop\]Model\_Number" value="" size="15">
     </td>
</tr>

**<!-- Static/Read Only Field Display  -->**
<tr>
     <td class="bold">Barcode # (Read Only)</td>
     <td>
     ~(\[Students.U\_Laptop\]Barcode)
     </td>
</tr>

**<!-- Static/Read Only Field Inside Input  Box -->**<tr>
     <td class="bold">Barcode # (Read Only)</td>
     <td>
     <input type="text" name="\[Students.U\_Laptop\]Barcode" value=""
     readonly="readonly">
     </td>
</tr>

**<!-- Radio Button -->**<tr>
     <td class="bold">Operating System</td>
     <td>
     <input type="radio" name="\[Students.U\_Laptop\]OS" value="Windows">Windows
     <input type="radio" name="\[Students.U\_Laptop\]OS" value="Mac">Mac
     </td>
</tr>

**<!-- Check Box -->**<tr>
     <td class="bold">Laptop Lost?</td>
     <td>
     <input type="checkbox" name="\[Students.U\_Laptop\]IsLost" value="1">
     </td>
</tr>

**<!-- Drop Down/Popup Menu -->**<tr>
     <td class="bold">Manufacturer</td>
     <td>
     <select name="\[Students.U\_Laptop\]Manufacturer">
          <option value="">Select a Company</option>
          <option value="Acer">Acer</option>
          <option value="Alienware">Alienware</option>
          <option value="Apple">Apple</option>
          <option value="Asus">Asus</option>
          <option value="Compaq">Compaq</option>
          <option value="Dell">Dell</option>
     </select>
     </td>
</tr>

**<!-- Text Area -->**<tr>
     <td class="bold">Damages Comments</td>
     <td>
     <textarea name="\[Students.U\_Laptop\]Damages\_Comments" cols="50" rows="5">
     </textarea>
     </td></tr>

## One-To-Many Table Extensions

A one-to-many table extension creates a child table to the designated parent table and allows multiple records to be created that are tied back to a single parent record. Examples of existing one-to-many tables in PowerSchool where the Students table is the parent are Special Programs, Logs, and Historical Grades. To view, store, and retrieve data from your own one-to-many tables use a special HTML tag called tlist\_child. This tag can auto-generate an HTML table to display rows of records from the designated child table, including an Add button and Delete button for each row you create.

### tlist\_child

The following is the format for the tlist\_child HTML tag.

~\[tlist\_child:<CoreTableName>.<ExtensionGroup>.<ExtensionTable>;displaycols:<List of Fields>;fieldNames:<List of Column Headers>;type:<FormatName>\]

The following provides additional information on this tag:

-   The **<CoreTableName>.<ExtensionGroup>.<ExtensionTable>** narrows the query down to a single child table. For example, a child table to track college applications could be STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS
-   The **displaycols** are a comma-separated list of columns from the one-to-many table, and can include any or all of the defined columns in that table. Two special ID columns may also be referenced: ID and <CoreTableName>DCID. In the college example above this would be STUDENTSDCID.
-   The **fieldNames** are a comma-separated list of the labels that should appear in the auto-generated HTML table heading. These labels may contain spaces. **Note:** If the nomenclature "displaycols" and "fieldNames" seems backwards, it might help to think of them as representing "display this set of columns from the table" and "names to be displayed next to each field in the user interface."
-   The **type** parameter specifies a format. Valid format options are "html" or "json"
    -   html:  Automatically generate an HTML table that allows for dynamic record creation and deletion.
    -   json:   The output of the tlist\_child will be a JSON array with an object name of "Results". It is not necessary to include fieldNames when using JSON. The first field in the array will always be the ID field.

Add the tlist\_child tag wherever you would like the table to appear on your page.

The following is an example for a one-to-many table to track  college applications:

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request Date,Status,Scholarship?,Completion Date,Outcome,Notes; type:html\]

  

![undefined](/webservices/developer/media/file/30)

## Independent Table Extensions

An independent table extension creates a table that is not associated with any existing PowerSchool table. Examples of existing independent tables in PowerSchool are States, CountryISOCodeLU, LocaleTimeFormat, and MIMETypes. To view, store, and retrieve data from your own independent tables use a special HTML tag called tlist\_standalone. This tag can auto-generate an HTML table to display your rows of records, including an Add button and Delete buttons for each row that has been created.

### tlist\_standalone

The following is the format for the tlist\_standalone HTML tag.

~\[tlist\_standalone:<ExtensionGroup>.<ExtensionTable>;displaycols:<List of Fields>;fieldNames:<List of Column Headers>;type:<FormatName>\]

The following provides additional information on this tag:

-   The **<ExtensionGroup>.<ExtensionTable>** narrows the query down to a single independent table. For example, an independent table created to maintain a master list of all higher education institutions could be U\_CollegeApp.U\_Institutions.
-   The **displaycols** are a comma-separated list of columns from the standalone table, and can include any or all of the defined columns in that table. A database record ID column may also be referenced using the name ID. This column is automatically created when the table is initially defined.
-   The **fieldNames** are a comma-separated list of the labels that should appear in the auto-generated HTML table heading. These labels may contain spaces.
-   The **type** parameter specifies a format. Valid format options are "html" or "json"
    -   html:  Automatically generate an HTML table that allows for dynamic record creation and deletion.
    -   json:   The output of the tlist\_standalone will be a JSON array with an object name of "Results". It is not necessary to include fieldNames when using JSON. The first field in the array will be the ID field.

Add the tlist\_standalone tag wherever you would like the table to appear on your page.

The following is an example for the master list of all higher education institutions:

~\[tlist\_standalone:U\_CollegeApp.U\_Institutions;displaycols:IPEDS\_ID,Institution\_Name,Institution\_Type,Phone,URL;fieldNames:IPEDS ID,Institution Name,Institution Type,Phone Number,Web Address;type:html\]

## Special Formatting of tlist\_child and tlist\_standalone Columns

### Default Behavior

By default all of the columns in an  auto-generated HTML table will be input fields approximately 20 characters wide  unless the extended field type is date or Boolean. Date fields include the  pop-up calendar widget. Boolean fields are displayed as check boxes. Special  code and tags have been created which allow the remaining input fields to be  changed to drop-down menus, radio buttons, text area or static text. It is  possible to modify the width of the input fields using Cascading Style Sheets.

**Important Note:** When using special formatting scripts, the field name used within the script must exactly match the field name used in the tlist\_child or tlist\_standalone tag. For example, if the tlist\_child displaycols lists the field name as “Institution” do not use “INSTITUTION” in the script code: tlistText2DropDown('INSTITUTION',InstValues).

Example screenshot of a table auto-generated by tlist\_child with special formatting applied:

![undefined](/webservices/developer/media/file/31)

### Drop-Down Menu Example

-   Within the <head> tag, add the tlistCustomization.js JavaScript file by including:  
     <script src="/scripts/tlistCustomization.js"></script>
-   Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
-   Directly after the tlist tag, use a script similar to the following example for field(s) you want to change from input text to a drop-down menu. The script will define the drop-down menu options that should be displayed to users and stored in the database. It will also define the variable name to be used to identify the list. Any variable name may be used. This script could be repeated if more than one field needs to be displayed as a drop-down menu. In this case unique variable names must be used for each. After the variable defines the value options, the following command completes the script:

tlistText2DropDown('<ColumnNameToReplace>',<JavaScript\_Variable\_Name>);

Please note that the "ColumnNameToReplace" is from the displaycols, not the fieldNames. If this seems backwards, please take a look at the definitions of [displaycols and fieldNames](#displaycols).

<!DOCTYPE  html>
<html>
<!-- start right frame -->
<head>
     <title>College Applications</title>
     ~\[wc:commonscripts\]
    <script src="/scripts/tlistCustomization.js"></script>
     <link href="/images/css/screen.css" rel="stylesheet" media="screen">
     <link href="/images/css/print.css" rel="stylesheet" media="print">
</head>
<body>
<form action="/admin/changesrecorded.white.html" method="POST">
~\[wc:admin\_header\_frame\_css\]<!-- breadcrumb start --><a href="/admin/home.html" target="\_top">Start Page</a> > <a href="home.html?selectstudent=nosearch" target="\_top">Student Selection</a> > College Applications<!-- breadcrumb end -->~\[wc:admin\_navigation\_frame\_css\]
~\[wc:title\_student\_begin\_css\]College Applications~\[wc:title\_student\_end\_css\]

<!-- start of content and bounding box -->
<div class="box-round">
~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request Date,Request Status,Scholarship,Completion Date,Outcome,Notes;type:html\]
    <script>
        var InstValues = {};
        InstValues\['1'\]='Option 1';
        InstValues\['2'\]='Option 2';
        InstValues\['3'\]='Option 3';
        InstValues\['4'\]='Option 4';
        InstValues\['5'\]='Option 5';
        InstValues\['6'\]='Option 6';
        InstValues\['7'\]='Option 7';
        InstValues\['8'\]='Option 8';
        InstValues\['9'\]='Option 9';
        InstValues\['10'\]='Option 10';
        tlistText2DropDown('Institution',InstValues);
    </script>
     <br>
     <div class="button-row">
     <input type="hidden" name="ac" value="prim">~\[submitbutton\]
     </div>
</div>
<br>

<!-- end of content of bounding box -->
~\[wc:admin\_footer\_frame\_css\]
</form>
</body>
</html><!-- end right frame -->    

### Drop-Down Menu Example (Value/Name Pair)

**Introduced in PowerSchool 8.0**

This version of the drop-down menu script differs from the original version by allowing control over both the value that is displayed in the drop-down menu and, separately, the value that is stored in the database.

-   Within the <head> tag, add the tlistCustomization.js JavaScript file by including:  
     <script src="/scripts/tlistCustomization.js"></script>
-   Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
-   Directly after the tlist tag, use a script similar to the following example for field(s) you want to change from input text to a drop-down menu. The script will define both the drop-down menu options that should be displayed to users and a chosen value to be stored in the database. It will also define the variable name to be used to identify the list. Any variable name may be used. This script could be repeated if more than one field needs to be displayed as a drop-down menu. In this case unique variable names must be used for each.

After the variable defines the value options, the following command completes the script:

tlistText2DropDownValNamePair('<FieldName>',<JavaScript\_Variable\_Name>);

**Note:** The following is the same code as the first drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request Date,Request Status,Scholarship,Completion Date,Outcome,Notes;type:html\]

<script>
	var InstValues = \[\];
	InstValues.push(\['C','Considering'\]);
	InstValues.push(\['W','Waitlist'\]);
	InstValues.push(\['A','Accepted'\]);
	InstValues.push(\['D','Denied'\]);
	InstValues.push(\['5','Option 5'\]);
	tlistText2DropDownValNamePair('Outcome',InstValues);
</script>

### Radio Button Example

-   Within the <head> tag, add the tlistCustomization.js JavaScript file by including:  
     <script src="/scripts/tlistCustomization.js"></script>
-   Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
-   Directly after the tlist tag, use a script similar to the following example for field(s) you want to change from input text to radio buttons. The script will define the radio buttons that should be displayed to users and be assigned a variable name. Any variable name may be used. This script could be repeated if more than one field needs to be displayed as a radio buttons. In this case unique variable names must be used for each. After the variable defines the value options, the following command completes the script:

tlistText2RadioButton('<FieldName>',<JavaScript\_Variable\_Name>);

**Note:** The following is the same code as the original drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request Date,Request Status,Scholarship,Completion Date,Outcome,Notes;type:html\]

<script>
	var rbValues = {};
	rbValues \['1'\]='Yes';
	rbValues \['2'\]='No';
	tlistText2RadioButton('Scholarship',rbValues);
</script>

### Radio Button Example (Value/Name Pair)

##### Introduced in PowerSchool 8.0

This version of the radio button script differs from the original version by allowing control over both the value that is displayed for the radio buttons and, separately, the value that is stored in the database.

-   Within the <head> tag, add the tlistCustomization.js JavaScript file by including:  
     <script src="/scripts/tlistCustomization.js"></script>
-   Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
-   Directly after the tlist tag, use a script similar to the following example for field(s) you want to change from input text to radio buttons. The script will define the radio buttons that should be displayed to users and be assigned a variable name. Any variable name may be used. This script could be repeated if more than one field needs to be displayed as a radio buttons. In this case unique variable names must be used for each. After the variable defines the options that should be displayed to users and a chosen value to be stored in the database, the following command completes the script:

tlistText2RadioButtonValNamePair('<FieldName>',<JavaScript\_Variable\_Name>);

**Note:** The following is the same code as the original drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request Date,Request Status,Scholarship,Completion Date,Outcome,Notes;type:html\]

<script>
	var rbValues = \[\];
	rbValues.push(\['Y','Yes'\]);
	rbValues.push(\['N','No'\]);
	tlistText2RadioButtonValNamePair('Scholarship',rbValues);
</script>

### Text Area Example

-   Within the <head> tag, add the tlistCustomization.js JavaScript file by including:  
     <script src="/scripts/tlistCustomization.js"></script>
-   Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
-   Directly after the tlist tag, use a script similar to the following example for field(s) you want to change from input text to a text area. The script will define the size of the text area that should be displayed to users and be assigned a variable name. This script could be repeated if more than one field needs to be displayed as a text area.

tlistText2TextArea('<Field\_Name>',<rows>,<columns>);

**Note:** The following is the same code as the original drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request Date,Request Status,Scholarship,Completion Date,Outcome,Notes;type:html\]

<script>
	tlistText2TextArea('Notes',4,50);
</script>

### Static/Read Only Text Example

-   Within the <head> tag, add the tlistCustomization.js JavaScript file by including:  
     <script src="/scripts/tlistCustomization.js"></script>
-   Within the <form> tag, use tlist\_child or tlist\_standalone tag to add the tlist auto-generated table.
-   Directly after the tlist tag, use a script similar to the following example for field(s) you want to change from input text to read only text. This script could be repeated if more than one field needs to be modified.

tlistText2StaticText('<Field\_Name>');

**Note:** The following is the same code as the original drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request  Date,Request Status,Scholarship,Completion Date,Outcome,Notes;type:html\]

<script>
	tlistText2StaticText('Institution');
</script>

### Static/Read Only After Submit Text Example

**Introduced in PowerSchool 8.0**

This new option allows a value to be entered for a new field when it is originally blank, but then makes that field read only.

-   Within the <head> tag, add the tlistCustomization.js JavaScript file by including:  
     <script src="/scripts/tlistCustomization.js"></script>
-   Within the <form> tag, use tlist\_child or tlist\_standalone tag to add the tlist auto-generated table.
-   Directly after the tlist tag, use a script similar to the following example for field(s) you want to change from input text to read only text once they are saved. This script could be repeated if more than one field needs to be modified.

tlistText2StaticTextAllowNew('<Field\_Name>');

**Note:** The following is the same code as the original drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request Date,Request Status,Scholarship,Completion Date,Outcome,Notes;type:html\]

<script>
	tlistText2StaticTextAllowNew('Institution');
</script>

### Multiple Special Formatting Tags Example

The following example shows all of the above examples used together.

**Note**: The following is the same code as the drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request Date,Request Status,Scholarship,Completion Date,Outcome,Notes;type:html\]

<script>
	var InstValues = {};
	InstValues\['1'\]='PowerSchool University';
	InstValues\['2'\]='College of Standards';
	InstValues\['3'\]='University of DDA';
	tlistText2DropDown('INSTITUTION',InstValues);
	var rbValues = {};
	rbValues \['1'\]='Yes';
	rbValues \['2'\]='No';
	tlistText2RadioButton('Scholarship',rbValues);
	tlistText2TextArea('Notes',4,50);
	tlistText2StaticText('Outcome');
</script>

### Using CSS Styles to Resize Input Fields

As previously noted, the default width of all fields in a tlist\_child or tlist\_standalone table, except Boolean fields which are shown as a checkbox, is 20 characters wide (about 180px). To adjust the size of individual columns in the auto-generated HTML table use Cascading Style Sheets (CSS).

Each column in the auto-generated HTML table will be part of a <colgroup> with each column in the table given a class attribute equal to "col-" plus the field name. In our college application tlist\_child example, the Institution <col> tag would include class="col-Institution". Set this value to approximately 20px higher than the input tag.

Each <input> tag in the table will be tagged with a class attribute equal to the field name. In our college application tlist\_child example, the Institution input tag would include class="Institution". Define CSS styles for these classes to control the width of the column.

The following example shows the definition of CSS styles for several columns in our tlist\_child table.

**Note**: Only the HTML code necessary to demonstrate this example has been included.

<html>
<head>
:
:
<style>

.col-Institution		{width:235px;}
.Institution			{width:215px;}
.col-Request\_Date		{width:110px;}
.Request\_Date			{width:90px;}
.col-Request\_Status	{width:80px;}
.Request\_Status		{width:60px;}
.col-Scholarship		{width:70px;}
.Scholarship			{width:50px;}

</style>
</head>
<body>
:
:
~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Date,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Request Date,Request Status,Scholarship,Completion Date,Outcome,Notes;type:html\]

## Using A Proper File Record Number (FRN) For Staff

In order to allow Teachers to access multiple schools using a single account with the Unified Teacher Record feature, it was necessary to split the TEACHERS table into two different tables – A USERS table to contain all data directly related to the user, and the SCHOOLSTAFF table to contain all data directly related to the Teacher/School relationship. For more detailed information see Knowledgebase article [**69896**](https://powersource.pearsonschoolsystems.com/article/69896), _Technical Information and Field List for  Unified Teacher Record_, available on PowerSource.

Understanding this new table structure for staff will be important when creating custom pages for staff that use the tlist\_child tag. When creating a link to the new custom page the proper FRN must be part of the link. Historically this has been done by adding "?frn=~(frn)" to the end of the link. For example:

<a href="schedulematrix.html?frn=~(frn)">

-   **schedulematrix.html** is the page we are linking to.
-   **?frn=** is located immediately after the page address.
-   **~(frn)** is a special tag that would insert the full proper FRN for the staff member currently being viewed on the staff page. An FRN consists of two parts; the 3-digit table number and the DCID field from that table.

In older versions of PowerSchool the ~(frn) tag would always be 005+Teachers.DCID. The Teachers table was table 005. So for a staff member with a DCID of 134 the ~(frn) would return 005134. Starting with PowerSchool 7.9 and the ability to create database extensions to the Users table (204) or SchoolStaff table (203), it is critical to create page links that pass the proper FRN. For example, you want to create a database extension to the Users table and a custom web page to track teacher credentials. After creating a U\_Certificates extension table use the following tlist\_child tag on your custom web page to create, view, and delete records.

~\[tlist\_child:**Users**.U\_Credentials.U\_Certificates;displaycols:CredNum,CredType,CredIssuer,CredStart,CredEnd;fieldNames:Credential Number,Credential Type,Credential Issuer,Start Date,Expires Date;type:html\]

Because the records on this page all relate to the Users table (table 204), construct your page link using "**204~(\[teachers\]USERS\_DCID)**" rather than ~(frn) or the tlist\_child table will not function properly. This is what the link might look like:

<a href="credentials.html?frn=204~(\[teachers\]USERS\_DCID)">Credentials</a>

This will ensure the records in the tlist\_child table use the DCID field from the Users table rather than the Teachers table.

-   PS SIS API
    -   [Security](#/page/security)
    -   [Data Access](#/page/data-access)
    -   [Resources](#/page/resources)
    -   [Events](#/page/events)
    -   [Single Sign On](#/page/single-sign-on-2)
    -   [Examples](#/page/examples)
-   PS SIS Customization
    -   [Page Customization and Database Extensions](#/page/page-customization-and-database-extensions)
    -   [PowerQuery DAT](#/page/powerquery-dat)
    -   [PowerTeacher Pro Customization](#/page/powerteacher-pro-customization)
    -   [PS-HTML tags](#/page/ps-html-tags)
    -   [Plugins](#/page/plugins)
    -   [Student Contacts Customization](#/page/student-contacts-customization)
    -   [New Experience Start Page Customization](#/page/new-experience-start-page-customization)
    -   [PowerSchool SIS Attendance in Schoology Customizations](#/page/powerschool-sis-attendance-in-schoology-customizations)
    -   [Enhanced Navigation Customization](#/page/user-interface-customization)
-   Community
    -   [Blog](#/page/blog)
    -   [Developer Forums](https://support.powerschool.com/forum/319)
    -   [Video Library](#/page/video-library)
# Developer Admin

Toggle navigation [PowerSource](/)

# Plugin XML

## Introduction

Plugin XML is a file for third-party application developers to use to declare and configure PowerSchool API services used by your application. Plugins have a number of other functions not related to PowerSchool API services such as the ability to create extended schema and include custom pages and page fragments. For more information on these capabilities, see the [PowerSchool Database Extension and Page Customization Developer Guide](https://support.powerschool.com/article/74828).

## Available Services

The following services can be defined in the plugin XML.

[Data Access](#/page/data-access)

Third-party application can request generation of OAuth client credential for provisioning data by declaring an `<oauth>` element.

[Linking](#tag_links)

Plugin can create general links to internal pages in PowerSchool or to external pages that do not require authentication by adding a `<links>` element.

[OpenID Single Sign-On](#/page/powerschool-as-openid-identity-provider)

Plugin can create OpenID single sign-on links to third-party applications by adding an `<openid>` element.

[SAML Single Sign-On](#/page/powerschool-as-saml-identity-provider)

Plugin can create SAML single sign-on links to third-party applications by adding a `<saml>` element.

[Registration](#/page/automatic-registration)

Plugin can ask PowerSchool to register itself by adding a `<registration>` element.

## XML Elements

The following illustrates the plugin elements hiearchy. Click a plugin element to view detailed information about the element.

-   [<plugin>](#tag_plugin)
-   -   [<registration>](#tag_registration)
    -   -   [<callback-data>](#tag_callback-data)
    -   [<oauth>](#tag_oauth)
    -   [<access\_request>](#tag_access_request)
    -   -   [<field>](#tag_field_request)
        
        -   [<cdn\_request>](#tag_cdn_request_request)
        -   -   [<cdn>](#tag_cdn_request)
    -   [<links>](#tag_links)
    -   -   [<link>](#tag_link)
        -   -   [<ui\_contexts>](#tag_ui_contexts)
            -   -   [<ui\_context>](#tag_ui_context)
    -   [<openid>>](#tag_openid)
    -   -   [<<links>](#tag_openid_links)
        -   -   [<link>](#tag_openid_link)
            -   -   [<ui\_contexts>](#tag_ui_contexts)
                -   -   [<ui\_context>](#tag_ui_context)
    -   [<saml>](#tag_saml)
    -   -   [<links>](#tag_saml_links)
        -   -   [<link>](#tag_saml_link)
            -   -   [<ui\_contexts>](#tag_ui_contexts)
                -   -   [<ui\_context>](#tag_ui_context)
        -   [<attributes>](#tag_saml_attributes)
        -   -   [<user>](#tag_saml_user)
            -   -   [<attribute>](#tag_saml_attribute)
        -   [<permissions>](#tag_saml_permissions)
        -   -   [<permission>](#tag_saml_permission)
    -   [<publisher>](#tag_publisher)
    -   -   [<contact>](#tag_contact)

### plugin

The outermost, or root element.Only one <plugin> element per XML configuration file is permitted.

Attribute

Description

Required?

xmlns

This attribute specifies that the XML document is using tags that exist in the http://plugin.powerschool.pearson.com namespace.  
Example: http://plugin.powerschool.pearson.com

Yes

xmlns:xsi

This attribute instructs the XML parser that this XML document is to be validated against a schema adhering to the W3C schema standards. This attribute defines the xsi namespace and schemaLocation attribute used for the next attribute.  
Example: http://www.w3.org/2001/XMLSchema-instance

No

xsi:schemaLocation

This attribute shows the location of the schema document, plugin.xsd, against which to validate the XML document for the target namespace http://plugin.powerschool.pearson.com. The location of the file can be any valid Uniform Resource Identifier (URI).  
Example: http://plugin.powerschool.pearson.com plugin.xsd

No

name

The name of the PowerSchool third-party plugin as it will appear in Plugin Management Configuration in PowerSchool. The name must be unique from already installed plugins. A maximum of 40 characters may be entered.

Yes

version

The version of the third-party plugin. A maximum of 20 characters may be entered.

No

description

The description of the third-party plugin as it will appear in Plugin Management Configuration in PowerSchool. A maximum of 256 characters may be entered.

No

**Optional Sub-elements:**

-   [<registration>](#tag_registration)
-   [<oauth>](#tag_oauth)
-   [<access\_request>](#tag_access_request)
-   [<links>](#tag_links)
-   [<openid>](#tag_openid)
-   [<saml>](#tag_saml)

**Mandatory Sub-element:**

-   [<publisher>](#tag_publisher)

### registration

This element triggers PowerSchool to [register](#/page/automatic-registration) itself to a third-party application. This is an optional element.

Attribute

Description

Required?

url

The URL of the registration web service provided by third-party application. the URL must be prefixed with "https://". A maximum of 1000 characters may be entered.

Yes

**Sub-elements:**

-   [<callback-data>](#tag_callback-data)

### callback-data

A node containing arbitrary data that will be included in [Registration Request](#/page/automatic-registration). It can contain the license key associated with a particular instance of plugin. This is an optional element.

**Attributes:** None

**Sub-elements:** None

### oauth

This element triggers PowerSchool to generate data access credentials for the plugin. This is an optional element. For more details, see [OAuth](#/page/oauth).

**Attributes:**

-   [accessLevelV1Api](#tag_accessLevelV1Api)

**Sub-elements:** None

**Example:**

<oauth accessLevelV1Api="READ"/>

### accessLevelV1Api

PowerSchool SIS 25.2.0.0+. This optional attribute defines the access level request to the v1 APIs. If not present then access to the v1 APIs will be denied. Available values are NONE (default is not present), READ, FULL.  
  
The FULL access level allows for updating Students (Student Fees/Test Scores) using the v1 API.  
  
**Note:** End users can change this value after plugin install from the **Data Provider Configuration** page.

### access\_request

This element is used to define access request fields for the plugin.  
  
This is an optional element.

**Attributes:** None

**Sub-elements:**

-   [<field>](#tag_field_request)
-   [<cdn\_request>](#tag_cdn_request)

**Example:**

<access\_request>
    <cdn\_request>
        <cdn name="https://code.jquery.com"/>
    </cdn\_request>
    
    <field table="STUDENTS" field="DCID" access="ViewOnly" />
    <field table="STUDENTS" field="SSN" access="ViewOnly" />
    <field table="STUDENTS" field="LAST\_NAME" access="FullAccess" />
    <field table="STUDENTS" field="FIRST\_NAME" access="FullAccess" />
    <field table="STUDENTCOREFIELDS" field="ACT\_DATE" access="FullAccess" />
    <field table="STUDENTCOREFIELDS" field="ACT\_COMPOSITE" access="FullAccess" />
    
</access\_request>

### field

This element defines individual fields in the access request. This is an optional element. There is no limit on maximum occurrences.  
  
**NOTE:** The access\_request feature does not apply to the /ws/v1/xxxx or PTG endpoints with the exception of the Students.SSN and Students.lunch\_status fields unless otherwise noted. The access\_request feature was not backported to existing API's in maintain backwards compatibility of v1 endpoints.

Attribute

Description

Required?

table

The name of a valid PowerSchool table. A maximum of 30 characters may be entered.

Yes

field

The name of a valid field in the PowerSchool table. A maximum of 30 characters may be entered.

Yes

access

The requested access for the field in the PowerSchool table. The value must be either ViewOnly or FullAccess.

Yes

### cdn\_request - PowerSchool SIS 21.11.0.0+

Element for defining external content servers as part of the PowerSchool SIS Content Security Policy (CSP) response headers. This is an optional element in the plugin.xml.  
  
CSP response headers tell the browser, among other things, what external resources it's allowed to load content from. CSP is an essential security feature to help protect against cross-site scripting (XSS), clickjacking, and other code injection attacks.  
  
For example, if your PowerSchool SIS instance is https://somesite.mydomain.org and you link to an external site that is not https://somesite.mydomain.org for content, such as for a JavaScript or image file, the browser will **not** trust the resource unless the host name is included in the CSP response header. The cdn\_request attribute allows for adding trusted hosts to the CSP header.  
  
**Note: Content Security Policy (CSP) headers are enabled as of PowerSchool SIS 22.7.0.0+.**  
  
**Note:** The CSP values defined in a plugin are global to the system, they do not currently apply to only the files included in the plugin.

**Sub-elements:**

-   [<cdn>](#tag_cdn_request)

### cdn

Element for defining an individual external content server. This is a mandatory element when a cdn\_request is defined. There is no limit on maximum occurrence.

**Attributes:**

Attribute

Description

Required?

name

The host name where the external content resides. A maximum of 50 characters may be entered. Starting with PowerSchool SIS 23.1 this has been increased to 300 characters.

Yes

### links

Element for adding navigational link. It is the container for individual <link> elements. This is an optional element.

**Attributes:** None

**Sub-elements:**

-   [<link>](#tag_link)

### link

This element defines the metadata for a single third-party navigation link in PowerSchool. This is a mandatory element.

Attribute

Description

Required?

display-text

The text of the physical link to the third-party application, as it appears in PowerSchool. A maximum of 100 characters may be entered.

Yes

path

URI of the link to third-party application. Clicking the link within PowerSchool will locate the URI. A maximum of 1000 characters may be entered.

Yes

title

Information about the link. For example, the text to display on a tooltip. A maximum of 256 characters may be entered.

No

**Sub-elements:**

-   [<ui\_contexts>](#tag_ui_contexts)

### ui\_contexts

Element for adding user interface location contexts to links. It is the container for individual <ui\_context> elements. This is a mandatory element.

**Attributes:** None

**Sub-elements:**

-   [<ui\_context>](#tag_ui_context)

### ui\_context

This element defines the user interface location for a single third-party navigation link in PowerSchool. This is a mandatory element.

Attribute

Description

Required?

id

The user interface location of the physical link to the third-party application. It must be one of the available [UI Contexts](#tag_ui_contexts)

Yes

For example, `<ui_context id="admin.left_nav"/>` specifies that the link is to appear in the main menu (left navigation) of the PowerSchool admin portal. Duplicate <ui\_context> tags will validate during the installation of the plugin, but the duplicates will be ignored. The possible ui\_context ids are:

id Value

Description

admin.left\_nav

This is the list of links under Applications headings in the main menu of the PowerSchool admin portal.

admin.header

This is the list of links in the dialog triggered by clicking the `Applications` icon in the navigation toolbar (header bar) of the PowerSchool admin portal.

teacher.header

This is the list of links in the dialog triggered by clicking the `Applications` icon in the navigation toolbar (header bar) of the PowerSchool teacher portal.

teacher.pro.apps

This is the list of links in the menu opened by clicking the `Apps` icon in the side navigation toolbar (charms bar) of the PowerTeacher Pro application.

guardian.header

This is the list of links in the dialog triggered by clicking the `Applications` icon in the navigation toolbar (header bar) of the PowerSchool guardian portal.  
  
**Note:** Links defined using this ui\_context will also show under the `Custom Links` section of Unified Classroom's `Quick Links` screen.

student.header

This is the list of links in the dialog triggered by clicking the `Applications` icon in the navigation toolbar (header bar) of the PowerSchool student portal.  
  
**Note:** Links defined using this ui\_context will also show under the `Custom Links` section of Unified Classroom's `Quick Links` screen.

**Sub-elements:** None

### openid

Element for adding [OpenID](#/page/powerschool-as-openid-identity-provider) relying party host and port and configuring single sign-on links. This is an optional elements.

Attribute

Description

Required?

host

This is the host name of the OpenID relying party. Specify only the host name, without any protocol or scheme, such as https. A maximum of 256 characters may be entered.

Yes

port

This is the port number of the OpenID relying party. The default value is 443.

No

**Sub-elements:**

-   [<links>](#tag_openid_links)

### links (OpenID)

Container element for single sign-on <link> elements. This is a mandatory element.

**Sub-elements:**

-   [<link>](#tag_openid_link)

### link (OpenID)

This element defines the metadata for a single OpenID single sign-on link in PowerSchool. This is a mandatory element.

Attribute

Description

Required?

display-text

The text of the OpenID single sign-on link to the third-party application, as it appears in PowerSchool. A maximum of 100 characters may be entered.

Yes

path

The path to a resource in the third-party application. This is the part of URL without host. A maximum of 1000 characters may be entered. The default value is `/`.

Yes

title

Information about the link. For example, the text to display on a tooltip. Max 256 characters.

No

**Sub-elements:**

-   [<ui\_contexts>](#tag_ui_contexts)

### saml

Element for adding a [SAML](http://saml.xml.org/) service provider or identity provider. This is an optional element.

**Attributes for PowerSchool as Identity Provider:**

Attribute

Description

Required?

name

The name of the service provider. A maximum of 256 characters may be entered. The name must be unique; there can be no other service provider configured with the same name in the same PowerSchool installation.

Yes

entity-id

The entity-ID of the service provider. A maximum of 256 characters may be entered.  
This value must be supplied by the service provider and must match the service provider value exactly. It will be used to identify the service provider when the service provider initiates a single sign-on.

Yes

base-url

The root-URL of the service provider application. A maximum of 256 characters may be entered.

Yes

metadata-url

The URL from which the PowerSchool SAML identity provider  
can obtain a copy of the service provider metadata. A maximum of 256 characters may be entered.

No

**Attributes for PowerSchool as Service Provider:**

Attribute

Description

Required?

name

The name of the service provider. In this case, the service provider is the local PowerSchool instance. You must provide a short name that will become part of the addresses used during SAML communication. A maximum of 256 characters may be entered.

Yes

idp-name

The name of the identity provider. This name would be provided by the organization that is setting up the identity provider. After plugin installation, this can be modified using the Plugin Detail page. A maximum of 40 characters may be entered. The name must be unique; there can be no other identity provider configured with the same name in the same PowerSchool installation.

Yes

idp-entity-id

The entity-ID of the identity provider. This would be provided by the organization that is setting up the identity provider. This is normally defined in the form of a URI. After plugin installation, this can be modified using the Plugin Detail page. A maximum of 256 characters may be entered. This value must be supplied by the identity provider and must match the identity provider value exactly.

Yes

idp-metadata-url

The URL from which the PowerSchool can obtain a copy of the identity provider metadata. A maximum of 1000 characters may be entered. After plugin installation, this can be modified using the Plugin Detail page.

Yes

**Sub-elements:**

-   [<links>](#tag_saml_links)

### links (SAML)

Container element for single sign-on <link> elements. This is a mandatory element.

**Sub-elements:**

-   [<link>](#tag_saml_link)

### link (SAML)

This element defines the metadata for a single SAML single sign-on link in PowerSchool. This is a mandatory element.

Attribute

Description

Required?

display-text

The text of the SAML single sign-on link to the third-party application, as it appears in PowerSchool. A maximum of 100 characters may be entered.

Yes

path

The path to a resource in the third-party application. The complete URL is constructed by prepending the [<saml>](#tag_saml) base-URL value to the path. A maximum of 1000 characters may be entered. The default value is `/`.

Yes

title

Information about the link. For example, the text to display on a tooltip. Max 256 characters.

No

**Sub-elements:**

-   [<ui\_contexts>](#tag_ui_contexts)

### attributes

Container element for single sign-on <user> elements. This is an optional element. For the list of supported attributes, see [PowerSchool as SAML Identity Provider](#/page/powerschool-as-saml-identity-provider).

**Attributes:** None

**Sub-elements:**

-   [<user>](#tag_saml_user)

### user

This element defines the type of users in PowerSchool. This is a mandatory element.

Attribute

Description

Required?

type

The type of users in PowerSchool. The possible values are:

-   admin
-   teacher
-   student
-   guardian

Yes

**Sub-elements:**

-   [<attribute>](#tag_saml_attribute)

### attribute

Container element for attributes. This is a mandatory element.

Attribute

Description

Required?

name

This element defines the name of the attribute in PowerSchool. A maximum of 256 characters may be entered.

Yes

attribute-name

An alias used in the SAML response being sent back to the service provider. A maximum of 256 characters may be entered.

No

attribute-value

A custom value, to be used by the SAML SSO services to pass through a constant value for this  
attribute or provide a custom parameter to be used to fetch dynamic values for this attribute from  
the database, to be delivered in the SAML response. A maximum of 256 characters may be entered.

No

**Sub-elements:** None

### permissions

Container element for single sign-on <permission> elements. This is an optional element.

**Attributes:** None

**Sub-elements:**

-   [<permission>](#tag_saml_permission)

### permission

Defines the metadata for a single SAML single sign-on link in PowerSchool. This is a mandatory element.

Attribute

Description

Required?

name

The name of the permission to be returned to the Service Provider (SP) with the SAML response. A maximum of 100 characters may be entered.

Yes

description

A user-friendly description of the permission, which shows up in the roles configuration page. A maximum of 1000 characters may be entered.

Yes

value

The value to be returned with this permission. This value will be returned with the name attribute as a name-value pair. A maximum of 100 characters may be entered.

Yes

**Sub-elements:** None

### publisher

Element for adding publisher information. This is a mandatory element.

**Attributes:**

Attribute

Description

Required?

name

The PowerSchool third-party plugin publisher name. A maximum of 100 characters may be entered.

Yes

**Sub-elements:**

-   [<contact>](#tag_contact)

### contact

Element to add publisher contact details. This is a mandatory element.

Attribute

Description

Required?

phone

The phone number of the publisher. A maximum of 20 characters may be entered.

No

email

The email address of the publisher. A maximum of 40 characters may be entered.

Yes

**Sub-elements:** None

-   PS SIS API
    -   [Security](#/page/security)
    -   [Data Access](#/page/data-access)
    -   [Resources](#/page/resources)
    -   [Events](#/page/events)
    -   [Single Sign On](#/page/single-sign-on-2)
    -   [Examples](#/page/examples)
-   PS SIS Customization
    -   [Page Customization and Database Extensions](#/page/page-customization-and-database-extensions)
    -   [PowerQuery DAT](#/page/powerquery-dat)
    -   [PowerTeacher Pro Customization](#/page/powerteacher-pro-customization)
    -   [PS-HTML tags](#/page/ps-html-tags)
    -   [Plugins](#/page/plugins)
    -   [Student Contacts Customization](#/page/student-contacts-customization)
    -   [New Experience Start Page Customization](#/page/new-experience-start-page-customization)
    -   [PowerSchool SIS Attendance in Schoology Customizations](#/page/powerschool-sis-attendance-in-schoology-customizations)
    -   [Enhanced Navigation Customization](#/page/user-interface-customization)
-   Community
    -   [Blog](#/page/blog)
    -   [Developer Forums](https://support.powerschool.com/forum/319)
    -   [Video Library](#/page/video-library)
# Developer Admin

Toggle navigation [PowerSource](/)

# Plugin ZIP

## Introduction

A plugin package is an archive that third-party application developers can create to declare and configure PowerSchool API services and their own [PowerQueries](#/page/powerqueries).

Plugins packages may also include a number of other functions not related to PowerSchool API services such as the ability to create extended schema and include custom pages and page fragments. For more information on these capabilities, see the [PowerSchool Database Extension and Page Customization Developer Guide](https://support.powerschool.com/article/74828).

Customization must be enabled in PowerSchool in order for the [PowerQuery](#/page/powerqueries) definitions to be loaded. If customization is disabled, the named queries will be installed but they will not be accessible. Customization is enabled in PowerSchool admin portal via `System > System Settings > Customization`.

## Package Name

The package may have any valid file name but must have the file extension `zip`.

## Package Folder Structure

The following is an example of the folder structure within the zip file:

|
+-- plugin.xml
|
+-- MessageKeys
    |
    \*-- example-plugin-message-keys.US\_en.properties
+-- pagecataloging
    |
    \*-- example-plugin-pagecataloging.json
+-- permissions\_root
    |
    \*-- partner.module1.permission\_mappings.xml
    \*-- partner.module2.permission\_mappings.xml
+-- queries\_root
    |
    \*-- partner.module1.named\_queries.xml
    \*-- partner.module2.named\_queries.xml
+-- web\_root
    |
    \*-- admin
        |
        \*-- home.partner.content.footer.txt

root

Contains only the [plugin.xml](#/page/plugin-xml). The file must be named exactly `plugin.xml` when it is part of a package.

MessageKeys

Contains all message key definition files. (Optional when defining PowerQueries only)

pagecataloging

Contains all [Page Cataloging](#/page/user-interface-customization) definition files. (Optional)

permissions\_root

Contains all [Permission Mappings](#/page/permission-mapping) definition files. (Optional when defining web pages/page fragments only)

queries\_root

Contains all [PowerQuery](#/page/powerqueries) definition files. (Optional when defining web pages/page fragments only)

web\_root

Contains all web page/page fragments. (Optional when defining PowerQueries only)

-   PS SIS API
    -   [Security](#/page/security)
    -   [Data Access](#/page/data-access)
    -   [Resources](#/page/resources)
    -   [Events](#/page/events)
    -   [Single Sign On](#/page/single-sign-on-2)
    -   [Examples](#/page/examples)
-   PS SIS Customization
    -   [Page Customization and Database Extensions](#/page/page-customization-and-database-extensions)
    -   [PowerQuery DAT](#/page/powerquery-dat)
    -   [PowerTeacher Pro Customization](#/page/powerteacher-pro-customization)
    -   [PS-HTML tags](#/page/ps-html-tags)
    -   [Plugins](#/page/plugins)
    -   [Student Contacts Customization](#/page/student-contacts-customization)
    -   [New Experience Start Page Customization](#/page/new-experience-start-page-customization)
    -   [PowerSchool SIS Attendance in Schoology Customizations](#/page/powerschool-sis-attendance-in-schoology-customizations)
    -   [Enhanced Navigation Customization](#/page/user-interface-customization)
-   Community
    -   [Blog](#/page/blog)
    -   [Developer Forums](https://support.powerschool.com/forum/319)
    -   [Video Library](#/page/video-library)
# Developer Admin

Toggle navigation [PowerSource](/)

# PowerQueries

## Introduction

A PowerQuery is a data source that can be accessed via the API. A typical PowerQuery declares a set of arguments, a set of columns, and a select statement. The arguments are optional, but the columns and select statement are required.

A PowerQuery may be pre-defined by PowerSchool or it may be defined by a third-party and installed in PowerSchool via the [Plugin Package](#/page/plugin-zip). Once the plugin is installed and enabled, the third-party PowerQuery becomes accessible as another resource in PowerSchool.

Unlike Custom Pages, PowerQueries do not require customization to be enabled in PowerSchool in order for them to become accessible. If customization is disabled, the PowerQueries are still accessible..

## Feature/Version Matrix

PowerQueries have had many features added over time and continue to have features added. This table makes it easier to know which features were available in which PowerSchool versions. Note that, in some cases, there are bug fixes or small enhancements that occurred later than the version given.

Feature

Summary

Min Version

Execute on Partner API

This was the basic PowerQuery execution.

8.2.0

Arg type=array

Arguments can be set as type=array or primitive to support e.g. filtering by a large numbers of students.

8.2.0

Count by special endpoint /count

Return count of rows that would be returned by the PowerQuery.

8.2.0

Table extensions on coreTable

Include table extensions on coreTable through the query string parameter "extensions".

8.2.0

Swagger 2.0

Allows getting summary and details of all installed PowerQueries (system and plugin-loaded). Note: This was the initial release; swagger features were added as time went on.

8.3.0

Changed name to PowerQuery

We made this name change in the User Interface and documentation because the previous term ("named query") was causing confusion. The internal files, etc. still use the term "named query."

9.0.0

Data Restriction Framework (DRF)

The Data Restriction Framework (patent pending) allows use of PowerQueries on internal APIs or anywhere the user type is not fully trusted to view all records.

9.0.0

Execute on Internal API

All execution of PowerQueries on internal APIs (i.e. for admin, teacher, guardian, or student portals), limiting data to what we believe can be shown according to FERPA. Required DRF.

9.0.0

DEM Export

Export results of almost any PowerQuery through Data Export Manager. Required DRF.

9.0.0

Ad-hoc row filter ($q)

Filter results of PowerQuery by any/all returned column values.

9.0.0

Order parameter

Dynamically order result of PowerQuery by any/all returned column values.

9.0.0

Projection parameter

Limit results returned to specific set of returned column values.

9.0.0

Student and Teacher selection handling

Makes it easy to include student and/or teacher selection in PowerQueries using the dofor query string parameter.

9.0.0

Streaming

Allow unlimited PowerQuery result set size.

9.0.0

Export in PDF, XLS, CSV

API-based export of PowerQuery results in commonly-needed formats.

9.0.0

Unique column alias required

Not really a feature so much as a requirement. Please see the [SQL section below](#unique_sql_column_name) for more details.

9.0.0

Count by parameter count=true

Another way to return results count along with results. Returns the row count that _would have been_ returned had the query not been paged.

9.1.0

Table extensions on all tables in query

Previously, extensions could only be returned on the declared coreTable. Now, any table in the query can be used, not just coreTable

9.2.0

Results alter selection

PowerQuery results can alter the student or teacher selection using the alterselection query string parameter.

9.2.0

PowerQueries against views

The most common views (including attendance and enrollment) now available.

9.2.0

Remove tables from Exclude List

Removed tables commonly needed for attendance reporting from the exclude list: PS\_MEMBERSHIP\_DEFAULTS, PT\_ENROLLMENT\_ALL, PT\_MEMBERSHIP\_DEFAULTS

9.2.0

Data Version (Delta Pull)

Added [Data Version](#data_version) pull capability.

10.0

Arg casing

Arguments can be set to be casing=lower or casing=upper to lower/upper case the values before being passed into the query. This only applies to String based arguments. Can be used with the arg type=primitive or type=array.

22.11.0.0

## PowerQuery File

The PowerQuery filename must follow this format:

\[name\].named\_queries.xml

The PowerQuery file may contain multiple named queries. The name could represent a group of or a set of related queries. Multiple physical PowerQuery files may also be created. All the files must be included in a [Plugin Package](#/page/plugin-zip). Here's an example of a simple PowerQuery file with a single PowerQuery defined:

<queries>
  <query name="com.mycompany.mydivision.students.get\_last\_name">
    <columns>
      <column column="students.first\_name">aliasstudents.aliasfirstname</column>
    </columns>
    <sql><!\[CDATA\[
       select first\_name from students
    \]\]></sql>
  </query>
  <!-- Define more queries here -->
</queries>

Though you can place only one or hundreds of PowerQueries in a single file, it is generally considered best practice to group PowerQueries together. So, if you need a set of queries for attendance, student info, and grades, you might want to have three files with the queries places in their respective files.

A note on naming: it is important that both the PowerQuery name and the file name be unique across the entire system. Both the file name and the query name are a single, flat namespace. Thus, it is highly recommended to use a file name like `com.powerschool.attendance.named_queries.xml` rather than just `attendance.named_queries.xml` so as not to conflict with other people writing attendance-related queries.

## PowerQuery Elements

### queries

Queries are the root element for the PowerQuery XML. It implies that one or more query elements may be defined in the file. This means that multiple named queries can be delivered in a single XML file.

### query

The query element describes all the components of a single PowerQuery. It is declared as shown below:

<query name="" coreTable="" flattened="" tags="">

It supports the following attributes:

Attribute

Description

Required

name

This attribute specifies the unique name of the query. The name should be a properly name spaced to prevent conflicts with other named queries already installed in the system. Use [snake\_case](http://en.wikipedia.org/wiki/Snake_case) when compound words are part of the name.  
Format:

name="\[com\].\[organization\].\[product\_name\].\[area\].\[name\]"

The query name should always have five parts: your two-part inverted domain name (com.organization) followed by a product name, followed by a general area of the system (attendance, grades, scheduling, etc.), followed by a description of the entities returned (meeting\_attendance, interim\_grades). **Please do not use "com.powerschool" as the first two parts of the name!** Use your own organization's name. This ensures that queries will not step on each other.

Yes

coreTable

When this attribute is included the PowerQuery can also provide extensions data when requested. Must be a valid table name in the PowerSchool schema.

No

flattened

PowerQuery supports two response formats. The default format groups fields by tables. The flattened does not group fields by tables and displays all the fields in the same level.

Possible values are:

-   true
-   false

No

tags

Tags are used for organizing and grouping named queries. Specify tags as a comma-delimited list. The fourth element of the query name is also used as a tag. For example, in a query with name "com.organization.product.assignment.grade\_detail", the "assignment" will be used as a tag.

No

### summary

The summary is an optional sub-element of the query. Although this is optional, it is highly recommended that you include a meaningful summary, as this information can be seen by end users in the PowerSchool Data Export Manager. Specify a short, but meaningful phrase or sentence describing the PowerQuery. Up to 50 characters may be used.

### description

The description is an optional sub-element of the query. Although this is optional, it is highly recommended that you include a meaningful description, as this information can be seen by end users in the PowerSchool Data Export Manager. Specify in as much detail as you can the behavior and restrictions of the PowerQuery.

### args

The args element is an optional sub-element of the query. If the select statement uses parameters, then they will be defined here as child [<arg>](#tag_arg) elements.

### arg

The arg element describes a single parameter to be used by the select statement. The arg element does not require a value and should always be specified as an empty element, but with supporting attributes as shown below:

<arg name="" required="" description="" type="" default="" casing="" />

It supports the following attributes:

Attribute

Description

Required

name

This is the name of the parameter. It must be unique within this PowerQuery. When used in the sql the name, case sensitive, will have a colon prefix. See [parameter(s)](#tag_param) for more information on the use of an arg in sql. **This name should be in the form of a legal Java identifier, per the [JPA specification](http://docs.oracle.com/cd/E12839_01/apirefs.1111/e13946/ejb3_langref.html#ejb3_langref_from_identifiers).**

Yes

column

This is the physical table and column name that the argument will be compared against. This attribute is optional, but is useful in providing additional validation on the input values. For example, if the column is a date column, then the value will be expected to be in the format YYYY-MM-DD. Must be a real table and column name if specified.

Required for the query to appear in Data Export Manager or when using the casing attribute.

required

This specifies if the parameter requires a value to be submitted at runtime. Possible values: true or false

No

description

Specify a description that may be useful to a user or developer that wants to use the PowerQuery. Like the other descriptions, this may show up in User Interfaces such as the Data Export Manager.

No, but highly recommended

type

A parameter may be a single value primitive, such as a single number or a single string, or it may be a list of values. Use "primitive" if the parameter expects a single value. Use "array" if the parameter expects a list of values. Possible values are: primitive or array. If "array" is used, the parameter's value should be in the form of a JSON array:

{ "primitiveArg": 1234, "arrayArg": \[1,2,3,4\] }

No

default

This is a default value that will be applied to the parameter if a value is not submitted when the PowerQuery is invoked. PowerSchool HTML tags are allowed as default values, though their use will prevent the PowerQuery from appearing in the Data Export Manager. Example use of Powerschool HTML tag as default:

default="~(curschoolid)"

No

casing

When working with string values it may be desired to "lower" or "upper" case the values being passed into the PowerQuery. The casing option allows this to be done server side. Prior to this option a developer may have used the following syntax, which works well for type=primitive:

lower(table.name) = lower(:argName)

However, when working with type=array args the consumer of the PowerQuery had to ensure that the casing of the values passed in are the same case the PowerQuery expected, otherwise the value would not be found:

lower(table.name) in (:argName)

Valid options are:

-   upper
-   lower
-   none - default if not defined

No

### columns

The columns element is a required sub-element of the query. Each column that will be returned by the select statement must be defined as a child [<column>](#tag_column) elements.

### column

The column element describes a single column that is expected to be returned by the select statement. The column value is typically the real table and column name, but it may also contain an alias that will be used in the response. Note that the order of the <column> elements is the same as the order of the actual columns returned in the SQL: if the first column returned from the SQL is STUDENTS.FIRST\_NAME, your first <column> element should be STUDENTS.FIRST\_NAME also.

A common declaration would be:

<column>STUDENTS.LASTFIRST</column>

An alias declaration would be:

<column column="STUDENTS.LASTFIRST">STUDENTS.FULLNAME</column> <!-- See below -->

The alias name can have one or two parts separated by a period. If it has only one, then you MUST specify a coreTable attribute. Note that this rule was not enforced before 9.2 (though we thought it was understood), so if you have pre-9.2 plugins, they will break, as evidenced by [several](https://support.powerschool.com/thread/15920) [threads](https://support.powerschool.com/thread/16317?66230) in the forums.  
  
**Note: Alias name parts should only contain A-z, 0-9, or underscore characters. Using spaces, for example, may cause issues when working with the PowerQueries in Data Export Manager.**

This element supports the following attributes:

Attribute

Description

Required

column

Specify a real table and column name in this attribute to use an alias. Must be a real table and column name if specified.

No

description

This is an optional description of the column. Specify a description that may be useful to a user or developer that uses this PowerQuery. Like the other descriptions, this may show up in User Interfaces such as the Data Export Manager.

No, but highly recommended

### sql

The sql element is where the select statement is defined. The select statement can be against any valid table in the PowerSchool schema. In PowerSchool version 9.2 and greater, select against views are supported.There are also sets of tables that are excluded and not supported. During the plugin enabling process any excluded table will be identified and will prevent the plugin from being enabled.

Nearly any SQL statement, including UNIONs and CTE/WITH Clause can be used. However, there are a few cautions.

First, ensure that your query always has unique column names. (This requirement was added in 9.0. It was required for many of the new 9.0 features, including dynamic sorting and $q filtering.) There are two problems in this example query that violate this rule:

\-- Bad, don't use this!
select students.first\_name,
       students.last\_name||'ski', 
       users.first\_name
from students inner join users on (...)
				

1.  The same column name "first\_name" is used twice. One of these must be aliased.
2.  The second column does not have a name. An alias must be provided.

This same query can be fixed by changing it to:

\-- OK to use
select students.first\_name,
       students.last\_name||'ski' as last\_name, 
       users.first\_name as users\_first\_name
from students inner join users on (...)
				

Next, to prevent potential encoding issues always enclose the select statement in a CDATA block. For example:

<sql>
<!\[CDATA\[
    select ...
    from ...
    where ...
    order by ...
\]\]>
<sql/>

**Note:** An order by clause should always be included in the sql, otherwise pagination against the PowerQuery will not work properly. Alternatively, you may apply [ad hoc ordering](#adhoc_ordering).  
  
PowerSchool HTML tags are currently not supported as part of the sql used in a PowerQuery. Use of PS-HTML tags will make the PowerQuery inaccessible. PowerSchool HTML tags can be used as part of an [arg](#tag_arg) default value, though their use will prevent the PowerQuery from appearing in the Data Export Manager.

### parameter(s)

Parameters are specified in the where clause using a colon prefix. The sql executed for a PowerQuery is done so as prepared statement. Because of this the parameter will act as a bind variable and data typing will be resolved automatically on execution. Parameters do not need to be manually escaped. Your PowerQuery sql can be tested in [SQL Developer](http://www.oracle.com/technetwork/developer-tools/sql-developer/overview/index.html) which will substitute parameters on execution like PowerQuery execution will. In the example below, student\_last\_name (String) is a parameter and it is being compared to the student.last\_name column (VARCHAR2):

where student.last\_name = :student\_last\_name

If extensions support is needed in the PowerQuery, the primary key column of the specified coreTable must be included in the SQL.

### Student/Staff selection

The sql element can also contain a reference to the student and/or staff selection in PowerSchool. This is a very powerful capability, because it allows the query author to easily allow the student/staff selection to be used when the query is invoked from the internal API. Even better, the inclusion of the selection is totally optional: if there is no selection when the query is invoked, or the caller does not ask for it, it works out to a no-op predicate. Here's an example of PowerQuery SQL defined to use the student selection:

<sql><!\[CDATA\[
		with selectedstudents as (select DCID,
          ID,
          LASTFIRST,
          FIRST\_NAME,
          LAST\_NAME,
          DOB,
          SCHOOLID,
          STUDENT\_NUMBER,
          STATE\_STUDENTNUMBER
		  from students where <@restricted\_table table="students" selector="selectedstudents"/>)
        select 
          DCID,
          ID,
          LASTFIRST,
          FIRST\_NAME,
          LAST\_NAME,
          DOB,
          SCHOOLID,
          STUDENT\_NUMBER,
          STATE\_STUDENTNUMBER
		from SELECTEDSTUDENTS
\]\]></sql>

If the query is invoked normally, the <@restricted\_table> construct will result in the string "1=1", thus acting as a no-op. However, when the following query string parameter is added (see section [Invoking a PowerQuery](#invoke) below for more details):

dofor=selection:selectedstudents

the construct will instead result in the following:

<sql><!\[CDATA\[
		with selectedstudents as (select DCID,
          ID,
          LASTFIRST,
          FIRST\_NAME,
          LAST\_NAME,
          DOB,
          SCHOOLID,
          STUDENT\_NUMBER,
          STATE\_STUDENTNUMBER
		  from students where (dcid in (:current\_student\_selection)))
        select 
          DCID,
          ID,
          LASTFIRST,
          FIRST\_NAME,
          LAST\_NAME,
          DOB,
          SCHOOLID,
          STUDENT\_NUMBER,
          STATE\_STUDENTNUMBER
		from SELECTEDSTUDENTS
\]\]></sql>

Please note that the keywords in the <@restricted\_table> construct must be all lower case. For example,

<@restricted\_table TABLE="students" selector="selectedstudents" /> <!-- Don't do this!!!! -->

will not work.

Here, "current\_student\_selection" is a parameter that is automatically filled in by PowerSchool. Note: This is NOT limited to 1,000 students/staff, though extremely large selections may slow down query execution.

Note that you don't need to use a WITH clause (though that is often the clearest way to express the query). Instead, you could simply embed the tag within the WHERE clause:

<sql><!\[CDATA\[
        select 
          DCID,
          ID,
          LASTFIRST,
          FIRST\_NAME,
          LAST\_NAME,
          DOB,
          SCHOOLID,
          STUDENT\_NUMBER,
          STATE\_STUDENTNUMBER
		from STUDENTS where <@restricted\_table table="students" selector="selectedstudents"/>
\]\]></sql>

Complex queries often are clearer with the tag in the WITH clause; for simple ones, the WHERE clause suffices. Oracle's optimizer will generally ensure the query execution is identical either way. You can specify the staff selection instead by using "SchoolStaff" for the table instead of "students". Finally, you can use both student and staff selection in the same query.

### Views

In PowerSchool version 9.2 and greater, PowerQueries can include PowerSchool views. The following are the requirements for using PowerSchool views:

-   Plugin [access request](#/page/access-request) must specify ViewOnly access for the fields accessed from views.
-   Views cannot be specified as coreTable attribute in query element.

The following list contains the PowerSchool views that can be read using PowerQuery:

View Name

PS\_ADAADM\_DAILY\_CTOD

PS\_ADAADM\_DAILY\_TTOD

PS\_ADAADM\_DEFAULTS\_ALL

PS\_ADAADM\_INTERVAL\_TTOD

PS\_ADAADM\_MEETING\_PTOD

PS\_ADAADM\_MEETING\_PTOP

PS\_ADAADM\_MEETING\_PTOP\_CURYEAR

PS\_ADAADM\_MEETING\_PTOP\_PREVYR

PS\_ADAADM\_MEETING\_TTOD

PS\_ADAADM\_MEETING\_TTOP

PS\_ADAADM\_MEETING\_TTOP\_CURYEAR

PS\_ADAADM\_MEETING\_TTOP\_PREVYR

PS\_ADAADM\_TIMEINTER\_TTOD

PS\_ADAADM\_TIME\_TTOD

PS\_ATTENDANCE\_DAILY

PS\_ATTENDANCE\_INTERVAL

PS\_ATTENDANCE\_MEETING

PS\_ATTENDANCE\_MEETINTER

PS\_ATTENDANCE\_TIME

PS\_ATTENDANCE\_TIMEINTER

PS\_CALENDAR\_DAY

PS\_ENROLLMENT

PS\_ENROLLMENT\_ALL

PS\_ENROLLMENT\_PROG

PS\_ENROLLMENT\_REG

PS\_ENROLLMENT\_SIF

PS\_MEMBERSHIP\_DEFAULT

PS\_MEMBERSHIP\_PROG

PS\_MEMBERSHIP\_REG

PS\_SCHOOLENROLLMENT

PT\_ENROLLMENT\_ALL

PT\_MEMBERSHIP\_DEFAULTS

## Invoking a PowerQuery

: Once the p lugin that includes the Powerquery is installed and enabled, it becomes available as another resource. A valid [access token](#/page/oauth) is needed to invoke the PowerQuery.

The PowerQuery request must be a POST to the resource path

\[your\_server\]/ws/schema/query/\[your\_named\_query\_name\]

.

The PowerQuery request should include the following header parameters:

Authorization: Bearer \[access\_token\]
Accept: application/json
Content-Type: application/json
			

The PowerQuery payload should always be a JSON structure. As best practice, an empty JSON object should be included even when the PowerQuery does not require any arguments or other parameters. Here's an example of invoking a PowerQuery that takes an argument called "studentnumber":

POST https://YOURSERVER/ws/schema/query/com.mycompany.mydivision.student.studentnumber
Accept: application/json
Content-Type: application/json
Authorization: Bearer xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

{
  "studentnumber": 1012973
}

The following query string parameters are supported:

Name

Default

Description

pagesize

100

The number of rows to return. You may set this value to anything you want, but if you set it to large values or 0 (i.e. unlimited), see the section on [streaming below](#streaming).

page

1

The page number of query results to return. Thus, the specific zero-indexed rows returned from the query are: (page-1)\*pagesize through (page\*pagesize)-1. If pagesize=0, this parameter is ignored.

order

N/A

The order to return query results in. If not passed, results will be returned in the order specified in the PowerQuery definition, if any. See the [ad hoc ordering](#adhoc_ordering) section for more information.

count

false

If true, also return the count of rows without respect to pagination. See [count](#count) below for more information.

dofor

None

If passed, limit query results to the student and/or staff selection. See [Limiting to the student and/or staff selection](#limiting_selection) below for more information.

### Ad-Hoc Row Restrictions ($q filter)

The payload may optionally include a `$q`parameter which is expected to be a FIQL string representing search restrictions to be applied in addition to any arguments. All FIQL operators are valid barring OR (,) and parenthetical expressions.

The FIQL expressions are expected to be in the form below to restrict a table in the PowerQuery statement.

\[tablename\].\[columnname\]\[fiql operator\]\[comparison value\]

Note that the tablename and columnname are the "logical" (or alias) table/column names in the query, not the physical (database) names. Usually these names would be the same, but there may be times that it's necessary to make them different. In that case, the $q parameter always uses the alias names. In this simple PowerQuery definition, for example:

<query name="com.mycompany.mydivision.students.get\_last\_name">
  <columns>
    <column column="students.first\_name">aliasstudents.aliasfirstname
  </columns>
  <sql><!\[CDATA\[
     select first\_name from students
  \]\]></sql>
</query>

One would write a $q query as follows using the alias, not the real, table and column names:

{
  "$q": "aliasstudents.aliasfirstname==Chauncey"
}

Alternatively, an extended table related directly to a table in the PowerQuery can be restricted using the syntax:

\[coretable\].\[extendedtablename\].\[columnname\]\[fiql operator\]\[comparison value\]

It is not permitted to submit a empty string as a comparison value. In order to restrict on the presence or absence of a null value, a unicode null character `\u0000`should be submitted as the comparison value.

Asterisks may be included in comparisons to varchar columns as wildcard characters.

In the event that a search for special characters such as `&`or `;`needs to be performed, this can be done by URL encoding the `[comparison value]`portion of the FIQL expression. Note that URL encoding the entire FIQL expression will result in an error so take special care to only encode the appropriate portion.

#### Examples

For the below examples, assume they are issued against a PowerQuery containing the Students and CC table.

_Restricting to students in eighth grade or higher, with a active enroll status, enrolled in course number MUSIC:_

"$q" : "students.grade\_level=ge=8;students.enroll\_status==0;cc.course\_number==MUSIC"

_Restricting to students with the phrase 'peanut butter' within the allergies column of the StudentCoreFields extended table:_

"$q" : "students.studentcorefields.allergies==\*peanut butter\*"

_Restricting to students with a blank middle\_name column:_

"$q" : "students.middle\_name==\\u0000"

_Restricting to students with the phrase 'M&Ms' within the allergies column of the StudentCoreFields extended table:_

"$q" : "students.studentcorefields.allergies==\*M%26Ms\*"

### Ad-Hoc Column Restrictions

> 9.2.0 and 9.2.1 both had problems with PowerQueries and the "projection" parameter. Please do not use the "projection" parameter in these PowerSchool versions.

The payload may optionally include a `projection`parameter which can restrict what columns are returned. The projection value must be a a semicolon-delimited list indicating the columns to be included in result. An asterisk ( `*`) may be supplied to specify that columns should be returned, but it is recommended to specify only necessary columns whenever possible. The expected format is:

\[tablename\].\[columnname\];\[tablename\].\[columnname\]

Note that the list of table columns are separated by a semicolon ( `;`) instead of a comma.

If the table name or column name provided in the projection is invalid, an error message will be returned.

#### Examples

For the below examples, assume they are issued against a PowerQuery containing the Students and CC table.

_Restricting to two columns in Students and one in CC:_

"projection" : "students.grade\_level;students.enroll\_status;cc.course\_number"

_Restricting to two columns in Students, two in StudentCoreFields plus all fields from SchoolsCoreFields:_

"projection" : "students.lastfirst;studentcorefields.act\_date;studentcorefields.act\_english;studentcorefields.dentist\_name;schoolscorefields.\*"

_No restriction, but also the default behavior if the parameter is excluded:_

"projection" : "students.\*;cc.\*"

### Ad-Hoc Ordering

PowerQueries can include "order by" clauses to give a default ordering. This is optional and overrides any existing "order by" clause in the PowerQuery's SQL. The ordering is passed in as a query string parameter called "order" that takes the same form as the other uses of this parameter in the API.

The value of the "order" parameter has the following format:

order = TABLE1.COLUMN1 \[;DIR\] \[ , ... \]

Any number of table.column pairs can be included. Ordering proceeds from left to right, with leftmost the most significant ordering. The optional specification ";DIR" affects order direction and can be either "asc" (ascending, the default) or "desc" (descending). Therefore:

order=students.last\_name,students.first\_name,students.entrydate;desc

Would order:

-   First, by ascending student last name
-   Next, within the same last name, by ascending student first name
-   Finally, within the same first and last name, by descending student entrydate

Please note that the table and column names used in the "order" parameter are, like the $q parameter, the "logical" table and column names or aliases (if they are used) rather than the real database table and column names.

### Return Row Count

When running a PowerQuery intended to page results for the user, the query is typically executed in two steps:

1.  Execute the query with the `/count` option to determine the total number of rows that would be returned if the query were not paged;
2.  Execute the query again to return the first page of results, which are then displayed to the user.

This can be time-consuming, especially if the query takes a significant amount of time to execute. For this purpose, the PowerQuery can be executed using the "count" query string parameter set to true, for example:

https://powerschool.com/ws/schema/query/com.pearson.core.guardian.student\_guardian\_detail?count=true&page=1

With the "count" option, the query returns the same rows it would have otherwise, but also returns the number of rows that _would have_ been returned, had the client not paged the query results.

{
   "name":"Students",
   "count":707625,
   "record":\[
      {
         "id":3328,
         "name":"Students",
         ...
      },
      ... Only first page of actual results returned
   \],
   "@extensions":"activities,u\_dentistry,studentcorefields,c\_studentlocator"
}

### Limiting to the student and/or staff selection

If a PowerQuery is written with the <@restricted\_table> construct as described above, the caller may request that the results be limited to the current student and/or staff selection. It may do this by using the "dofor" query string parameter:

dofor=selection:SELECTOR1 \[ , ... \]

That is, one or more selector strings, separated by commas. The selector strings must match with a selector from the PowerQuery's SQL statement to have any effect. In the example above, with `<@restricted_table table="students" selector="selectedstudents"/>`, the caller would be expected to supply the query string parameter:

dofor=selection:selectedstudents

The specific name "selectedstudents" has no significance (except that it has to match), but would be a typical name to use. This request will not have the expected effect in several situations:

-   Since selections only exist on the Internal API, this request has no effect when invoked over the Partner API.
-   If the PowerQuery was not written with a <@restricted\_table> construct, the request has no effect
-   If the PowerQuery was written with a <@restricted\_table> construct for the student selection, but is invoked with a request to filter by the staff selection (or vice versa), it has no effect
-   If the selector name does not match any selector in a <@restricted\_table> construct, it has no effect.
-   If the selector does match a selector in a <@restricted\_table> construct, but there is currently no student/staff selection, no rows will be returned (consistent with a selection of 0 students/staff).

Finally, note that the "dofor" query string parameter is active for the following endpoints:

POST /ws/schema/table/QUERYNAME
POST /ws/schema/table/QUERYNAME/count

### Data Version

The Data Version feature was released in PowerSchool 10.0.

Data version parameters are used to retrieve new or modified records from the PowerQuery based on a [Data Version Subscription](#/page/data-version-delta-pull#data_version_subscription).

The following list describes the data version parameters:

Name

Description

$dataversion

The data version number to use in filtering the results of the query.

$dataversion\_applicationname

The data version application name that should be applied in filtering the results of the query.

Below is an example usage of the data version parameters:

{ "$dataversion" : "16314148",
  "$dataversion\_applicationname" : "StudentChanges" }            
			

Both parameters must be specified together if data version parameters are used. And when data version parameters are used, the PowerQuery response will include the current or latest data version number. Below is an example response:

{
    "name": "USERS",
    "$dataversion": "16404886",
    "record": \[
        {
            "id": 112,
...
			

To use data version effectively in PowerQueries, the SQL should use outer joins instead of inner joins. When inner joins are used, it means that all tables in the SQL and in the subscription must have changed in order for that row to be returned in the PowerQuery. When outer joins are used, then rows will be returned if at least one table in the SQL and in the subscription has changed.

## Security

### External

PowerQueries can be invoked by an external system using [OAuth](#/page/oauth). The following are the requirements for external invocation:

-   Request must include an [OAuth](#/page/oauth) access token
-   Plugin declaration must include [access request](#/page/access-request) for all tables and fields needed

A 403 error will be returned if a PowerQuery attempts to access fields that:

-   Have not been included in the [access request](#/page/access-request)
-   Are [excluded](#/page/exclude-list) By PowerSchool

All 403 errors are logged.

### Internal

PowerQueries can also be invoked internally once a user has authenticated in the PowerSchool portals: admin, teacher, guardian, or student. This uses [Session Cookie](#/page/session-cookie) authentication. The following are the requirements for internal invocation:

-   User must be authenticated in the portal (request includes a session cookie)
-   Plugin package must include permissions mappings for the PowerQuery

A 403 error will be returned if there are no permission mappings defined for the PowerQueries.

PowerQueries will respond with a limited set of columns and rows because:

-   Field Level Security may restrict the columns visible to the authenticated user
-   [Data Restriction Framework](#/page/data-restriction-framework) may restrict the rows visible to the authenticated user. The [PowerQueries section of the DRF page](#/page/data-restriction-framework#powerqueries) describes in detail how PowerQueries are processed by the DRF.

## Streaming Results

PowerQuery results are paged by default. However, you may request all results to be streamed by including the query parameter `pagesize=0`. For example:

https://powerschool.com/ws/schema/query/com.pearson.core.guardian.student\_guardian\_detail?pagesize=0
			

When requesting streamed results, your client code must be coded to handle the stream. Example code for a few platforms are in the following sections.

### Example Perl Client

It's relatively straightforward in Perl using the very common LWP library. The subroutine streamFunc handles each chunk as it arrives. There does not appear to be any control over the size of what arrives.

my $req = HTTP::Request->new( POST => $uri );
$req->header( 'Content-Type' => 'application/json' );
$req->header( 'Authorization' => 'Bearer '. $access\_token );
$req->header( 'Accept' => 'application/json' );
$req->content( $json );
my $response = $browser->request( $req, ':content\_cb' => \\&streamFunc );

sub streamFunc {
  my ( $chunk, $res, $proto ) = @\_;
  processChunk( $chunk );
}
			

### Example PHP Client

Like everything else in PHP, you can do it, but it takes a hack to do it. In this case, you create a class that is registered as a protocol handler, and then use the curl option CURL\_FILE and pretend it's a file:

class MyStream {
  protected $buffer;
  function stream\_open($path, $mode, $options, &$opened\_path) {
  // Has to be declared, it seems...
    return true;
  }
  public function stream\_write($chunk) {
   ... process the chunk here ...
  }
}
// Register the wrapper, to be used with the pseudo-protocol "test"
stream\_wrapper\_register("test", "MyStream") or die("Failed to register protocol");

// Open the "file"
$fp = fopen("test://MyTestVariableInMemory", "r+");

// Configuration of curl
$ch = curl\_init(); c
url\_setopt($ch, CURLOPT\_URL, "http://www.rue89.com/");
curl\_setopt($ch, CURLOPT\_HEADER, 0);
curl\_setopt($ch, CURLOPT\_BUFFERSIZE, 256);
curl\_setopt($ch, CURLOPT\_RETURNTRANSFER, true);
curl\_setopt($ch, CURLOPT\_FILE, $fp); // Data will be sent to our stream ;-)
curl\_exec($ch);
curl\_close($ch);
// Don't forget to close the "file" / stream
fclose($fp);
			

Reference: [http://stackoverflow.com/questions/1342583/manipulate-a-string-that-is-30-million-characters-long/1342760#1342760](http://stackoverflow.com/questions/1342583/manipulate-a-string-that-is-30-million-characters-long/1342760#1342760)

### Example Node.js Client

Chunking is normal and automatic behavior in node.

var req = http.request(options, function(res) {
  res.setEncoding('utf8');
  res.on('data', function (chunk) {
    console.log(chunk.length);
  });
  req.on('error', function(e) {
    console.log("error" + e.message);
  });
});
req.end();
			

Reference: [http://stackoverflow.com/questions/22015673/node-js-http-request-returns-2-chunks-data-bodies](http://stackoverflow.com/questions/22015673/node-js-http-request-returns-2-chunks-data-bodies)

### Example Ruby Client

In Ruby, the standard httpclient library supports chunking natively, just like in node.js.

clnt = HTTPClient.new('http://myproxy:8080')
clnt.get\_content('http://dev.ctor.org/') do |chunk|
  puts chunk // Do whatever kind of processing you need
end
			

Reference: [http://www.rubydoc.info/gems/httpclient/2.1.5.2/HTTPClient](http://www.rubydoc.info/gems/httpclient/2.1.5.2/HTTPClient)

### Example Java Client

The Apache httpclient component library supports streaming as a java InputStream:

HttpClient httpclient = new HttpClient();
GetMethod httpget = new GetMethod("http://www.myhost.com/");
try {
  httpclient.executeMethod(httpget);
  Reader reader = new InputStreamReader(
          httpget.getResponseBodyAsStream(), httpget.getResponseCharSet());
  // consume the response entity
} finally {
  httpget.releaseConnection();
}
			

Reference: [http://hc.apache.org/httpclient-3.x/performance.html#Request\_Response\_entity\_streaming](http://hc.apache.org/httpclient-3.x/performance.html#Request_Response_entity_streaming)

-   PS SIS API
    -   [Security](#/page/security)
    -   [Data Access](#/page/data-access)
    -   [Resources](#/page/resources)
    -   [Events](#/page/events)
    -   [Single Sign On](#/page/single-sign-on-2)
    -   [Examples](#/page/examples)
-   PS SIS Customization
    -   [Page Customization and Database Extensions](#/page/page-customization-and-database-extensions)
    -   [PowerQuery DAT](#/page/powerquery-dat)
    -   [PowerTeacher Pro Customization](#/page/powerteacher-pro-customization)
    -   [PS-HTML tags](#/page/ps-html-tags)
    -   [Plugins](#/page/plugins)
    -   [Student Contacts Customization](#/page/student-contacts-customization)
    -   [New Experience Start Page Customization](#/page/new-experience-start-page-customization)
    -   [PowerSchool SIS Attendance in Schoology Customizations](#/page/powerschool-sis-attendance-in-schoology-customizations)
    -   [Enhanced Navigation Customization](#/page/user-interface-customization)
-   Community
    -   [Blog](#/page/blog)
    -   [Developer Forums](https://support.powerschool.com/forum/319)
    -   [Video Library](#/page/video-library)
# Developer Admin

Toggle navigation [PowerSource](/)

# PowerQuery DAT

## Introduction

With the release of PowerSchool SIS 22.9.0.0 it is now possible to create your own simple Data Access Tags for Students and Teachers. This is possible by leveraging specially crafted PowerQueries. PowerQuery DATs (PQD) are generally designed for single field/row results, such as a Student's Email. The PowerQuery DAT also allows for multiple fields/rows to be returned, such as the Course and count of missing assignments for the current year.  
  
NOTE: PowerQuery DATs, stock or custom, that utilize multiple fields and/or rows have limited support. Utilizing multiple fields and/or rows may not work correctly or impact expected formatting depending on where the DAT is utilized. For example if a DAT produces multiple fields and/or rows is used in an export it will shift columns and rows which may result in the file being unreadable by the system consuming the export.

An example plugin has been created to demonstrate how to hook into the new framework. Click the following link to download the plugin: [PQ\_DAT\_Example](/webservices/developer/media/file/162)

## Key Terms

-   **Data Access Tag (DAT)** - PowerSchool SIS specific function that allows the exporting of specific data.
-   **PowerQuery** - A [PowerQuery](#/page/powerqueries) may be pre-defined by PowerSchool or it may be defined by a third-party and installed in PowerSchool.
-   **Permission Mapping** - A [Permission Mapping](#/page/permission-mapping) is used to help control the security of the DAT. Not all fields can be secured using Field Level Security, due to this the `*powerquery` DAT requires the user have at least view access to the page linked to the PowerQuery.
-   **PowerQuery Arguments (arg)** - The PowerQuery [arg](#/page/powerqueries#tag_arg) allows for injection of several key values.

## Framework Information

### Security

Due to PowerQuery DATs leveraging the PowerQuery framework there are a number of built in security measures. These include Data Restriction Framework (DRF) and Field Level Security. Even with these measures it's possible to return the wrong data if there are coding errors in the SQL. This is due to the DRF rules being applied to the current user, not to the specific Student/Teacher record that is currently loaded.  
  
**It is the responsbility of the implementer to ensure that the SQL is correctly structured and limits the result to just the Student or Teacher the DAT is being run against. The SQL/PowerQuery should also be performant.**

One concern with user definable PowerQueries being used in a DAT is that not all fields can have FLS applied to them. This is handled by requiring the user to have at least View access to the page defined on in the Permission Mapping file.

### PowerQuery Definition

PowerQuery DATs require specifically defined attributes as part of the `query` element.

-   `flattened` = true
-   `dat` = true
-   `coreTable` = Students or SchoolStaff

For example:

<query name="com.example.custom.dats.students.student\_email"
                       flattened="true"
            		   coreTable="Students"
            		   dat="true">

### Argument Injection

When processing a PowerQuery from a DAT the SIS needs to know what arguments/data filters to pass down to the PowerQuery itself. There are a number of predefined arguments that will be injected automatically at run time if they are defined on the PowerQuery.  
  
**Notes:**

-   Prior to PowerSchool SIS 22.11.0.0 PowerQuery DATs did not support user defined arguments.
-   Argument injection is case insensitive.

Mappable PowerQuery Arguments

Description

coretabledcid

The currently selected Students.DCID or Teachers.DCID (SchoolStaff.DCID) value.  
  
This is the prefered method of associating the data to be returned is for the current record.  
  
Example: `Students.dcid = :coretabledcid`

curstudid

The currently selected Students.ID.  
  
Example: `Students.id = :curstudid`

curtchrid

The currently selected Teachers.ID (SchoolStaff.ID).  
  
Example: `SchoolStaff.id = :curtchrid`

curyearid

The currently selected Year ID, to be used in conjunction with coretabledcid, curstudid, or curtchrid.  
  
Example: `CC.TermID between (:curyearid * 100) and (:curyearid * 100) + 99`

curtermid

The currently selected Year ID, to be used in conjunction with coretabledcid, curstudid, or curtchrid.  
  
Example: `CC.TermID = :curtermid`

curschoolid

The currently selected School ID, to be used in conjunction with coretabledcid, curstudid, or curtchrid.  
  
Example: `CC.Schoolid = :curschoolid`

listaggdelim

The delimiter value to be used in a LISTAGG.  
  
Example: `LISTAGG(RACECD, :listaggdelim)`

User Defined

Starting with PowerSchool SIS 22.11.0.0 user defined arguments can be used in a PowerQuery DAT. These argument values are passed into the PowerQuery DAT by prefixing **arg.** in front of the argument name.  
**Notes:**

-   The **arg.** prefix cannot be used to override one of the built in argument injections.
-   When an argument is a date, the expected format to use in the PowerQuery DAT is MM/DD/YYYY.
-   When the PowerQuery arg is `type="array"`, and the argument is used as part of an IN clause, the values passed into the PowerQuery DAT are comma separated.
-   The PowerQuery arg `casing` attribute is new to PowerSchool 22.11.0.0. More information on this can be found [here](/developer/#/page/powerqueries#tag_arg).

#### Example 1 - Primitive Arg Type:

PowerQuery Argument:

<arg name="startDate" column="Attendance.Att\_Date" />

Argument in PowerQuery SQL:

WHERE att\_date >= :startDate

PowerQuery DAT:

~(\*powerquery;query=com.mycompany.mydivision.my\_date\_based\_pqd;**arg.startDate=7/1/2021**)

#### Example 2 - Array Arg Type:

PowerQuery Argument:

<arg name="attendanceCodes" column="Attendance\_Code.Att\_Code" type="array" casing="lower" />

Argument in PowerQuery SQL:

WHERE lower(att\_code) in ( :attendanceCodes )

PowerQuery DAT:

~(\*powerquery;query=com.mycompany.mydivision.my\_att\_code\_based\_pqd;**arg.attendanceCodes=A,T**)

## Using a PowerQuery DAT - Basic

### Basic Usage

For basic execution where a single row/field is returned the PowerQuery DAT simply takes a `query` param with the name of the PowerQuery. For a current list of stock PowerQuery DATs please see the PowerSchool Help for your current PowerSchool SIS version. The latest list of PowerQuery DATs can be found [here](https://docs.powerschool.com/PSHSA/latest/page-and-data-management/data-access-tags/powerquery-dats).  
  

## Using a PowerQuery DAT - Advanced

### Aggregated Results - `listaggdelim`

In some cases, such as the Race Codes DAT, a user may want to utilize LISTAGG to have multiple row values aggregated into a single result/list. This poses an issue as normally LISTAGG would have a delimiter hard coded in the SQL. By using the `listaggdelim` DAT param, in combination with a listaggdelim PQ arg, we can pass in the delimiter. The following are supported delimiters.

-   `dlf` (double line-feed)
-   `lf` (line-feed)
-   `p` (HTML paragraph: <p>)
-   `br` (HTML break: <br>)
-   `comma` (, )
-   `barecomma` (,)
-   `semicolon` (;)
-   `space` ( )
-   `pipe` (|)
-   `dash` ( - )

~(\*powerquery;query=com.powerschool.core.dats.students.racecodes;listaggdelim=br)

### Delimiters - `value-delim` / `row-delim`

It's possible to have the DAT output multiple columns and multiple rows. Such as on a Form Letter or Object Report. There are a few caveats to doing this:

-   The `fields` DAT param should be used to indicate what fields to return from the PowerQuery and in what order.
-   A max of 100 rows will be returned.
-   If the user running the report does not have access to a field defined in the PowerQuery, even if it's not in the `fields` param, then no data will be returned. Instead only \*\*\*\*\* will be returned. This is due to how the system handles user defined PowerQueries.

The delimiters used may need to be changed based on where the DAT is used. By default `value-delim` uses the **space** delimiter and `row-delim` uses the **lf** delimiter. The following delimiters are supported:

-   `dlf` (double line-feed)
-   `lf` (line-feed)
-   `p` (HTML paragraph: <p>)
-   `br` (HTML break: <br>)
-   `comma` (, )
-   `barecomma` (,)
-   `semicolon` (;)
-   `space` ( )
-   `pipe` (|)
-   `dash` ( - )

**NOTE:** The `value-delim` / `row-delim` options are ignored when the `tableformat` parameter is defined.

~(\*powerquery;query=com.powerschool.core.dats.students.missing\_assignment\_counts\_year;value-delim=dash;fields=course\_name,missingAsmtCount)

### Fields - `fields`

It's possible to have the DAT output multiple columns and multiple rows. Such as on a Form Letter or Object Report. There are a few caveats to doing this:

-   A max of 100 rows will be returned.
-   If the user running the report does not have access to a field defined in the PowerQuery, even if it's not in the `fields` param, then no data will be returned. Instead only \*\*\*\*\* will be returned. This is due to how the system handles user defined PowerQueries.

The `fields` parameter allows for the defining of what fields from the PowerQuery should be returned and in what order. The field names need to match the returned alias as defined in the `<column>value</column>` attribute of the PowerQuery. If the `fields` parameter is omitted then all the fields will be returned.  
  
For example the com.powerschool.core.dats.students.missing\_assignment\_counts\_year PowerQuery returns course\_number, course\_name, and missingAsmtCount however you may wish to only display course\_name and missingAsmtCount:

~(\*powerquery;query=com.powerschool.core.dats.students.missing\_assignment\_counts\_year;value-delim=dash;fields=course\_name,missingAsmtCount)

### No Row / Value Message - `no-rows-message` / `no-value-message`

Using the `no-rows-message` and `no-value-message` parameters it's possible to supply default text if there is value returned.  
  
As an example a user may wish to indicate `No Email` if a student does not have an email defined. As no row will exist we can use the `no-rows-message` to return No Email:

~(\*powerquery;query=com.powerschool.core.dats.students.student\_email;no-rows-message=No Email)

  
When returning multiple fields the `no-value-message` may be used to subsitiue the value if the value is empty. As an example it may be appropriate to return N/A using `no-value-message=N/A`

### Table Format - `tableformat`

It's possible to have the DAT output multiple columns and multiple rows. Such as on a Form Letter or Object Report. There are a few caveats to doing this:

-   The `tableformat` DAT param instructs the DAT to wrap the row/fields in TR/TD tags.
-   The table start/header/dat/table close **must** be on the same line. See example below.
-   The `fields` DAT param should be used to indicate what fields to return from the PowerQuery and in what order.
-   A max of 100 rows will be returned.
-   If the user running the report does not have access to a field defined in the PowerQuery, even if it's not in the `fields` param, then no data will be returned. Instead only \*\*\*\*\* will be returned. This is due to how the system handles user defined PowerQueries.

<table border="1"><tr><th width="200">Course Name</th><th width="50">Course Num</th><th width="30">Grade</th><th width="30">Term</th></tr>^(\*powerquery;query=com.example.custom.dats.students.storedgrades;tableformat;fields=course\_name,course\_number,grade,storecode)</table>

### Example PowerQuery from plugin

The example provided below, is the PQDatExample.named\_queries.xml file included in the provided example plugin above.

                <queries>
                <query name="com.example.custom.dats.students.student\_email"
                       flattened="true"
            		   coreTable="Students"
            		   dat="true">
                    <summary>DAT - Example - Student Email</summary>
                    <description>Get the Students Email for PowerQuery DAT.</description>
                    <args>
                        <arg name="coreTableDcid" column="Students.DCID" required="true" />
                    </args>
                    <columns>
                        <column column="emailaddress.emailaddress">emailaddress</column>
                    </columns>
                    <sql><!\[CDATA\[
            			select email.emailaddress from (select emailaddress.emailaddress
            			from emailaddress
            			join personemailaddressassoc on emailaddress.emailaddressid = personemailaddressassoc.emailaddressid and personemailaddressassoc.isprimaryemailaddress = 1
            			join person on personemailaddressassoc.personid = person.id
            			join students on person.id = students.person\_id
            			where students.dcid = :coreTableDcid
            			order by personemailaddressassoc.emailaddresspriorityorder asc) email
            			where rownum = 1
                  \]\]></sql>
                </query>
                <query name="com.example.custom.dats.students.storedgrades"
                       flattened="true"
            		   coreTable="Students"
            		   dat="true">
                    <summary>DAT - Example - StoredGrades For Student</summary>
                    <description>Get the StoredGrades for PowerQuery DAT.</description>
                    <args>
                        <arg name="coreTableDcid" column="Students.DCID" required="true" />
                    </args>
                    <columns>
                        <column column="StoredGrades.Course\_Name">Course\_Name</column>
                        <column column="StoredGrades.Course\_Number">Course\_Number</column>
                        <column column="StoredGrades.Grade">Grade</column>
            			<column column="StoredGrades.StoreCode">StoreCode</column>
                    </columns>
                    <sql><!\[CDATA\[
            			select storedgrades.course\_name, storedgrades.course\_number, storedgrades.grade, storedgrades.StoreCode
                        from students
                        join storedgrades on storedgrades.studentid = students.id
                        where students.dcid = :coreTableDcid
                  \]\]></sql>
                </query>
            </queries>

### Example Permissions file from plugin

The example provided below, is the PQDatExample.permission\_mappings.xml file included in the provided example plugin above.

                    <permission\_mappings>
                        <permission name="/admin/students/emailconfig.html">
                    		<implies allow="post">/ws/schema/query/com.example.custom.dats.students.student\_email</implies>
                        </permission>
                        <permission name="/admin/students/previousgrades.html">
                    		<implies allow="post">/ws/schema/query/com.example.custom.dats.students.storedgrades</implies>
                        </permission>
                    </permission\_mappings>

### Core PowerQuery DAT SQL as of 22.9.0.0

Below is the Core PowerQuery DAT SQL as of 22.9.0.0

<queries>
    <query name="com.powerschool.core.dats.students.student\_email"
           x-ps-version="22.9.0"
           flattened="true"
		   coreTable="Students"
		   dat="true">
        <summary>DAT - Student Email</summary>
        <description>Get the Students Email for PowerQuery DAT.</description>
        <args>
            <arg name="curStudId" column="Students.ID" required="true" />
        </args>
        <columns>
            <column column="emailaddress.emailaddress">emailaddress</column>
        </columns>
        <sql><!\[CDATA\[
			select emailaddress from (select emailaddress
			from emailaddress
			join personemailaddressassoc on emailaddress.emailaddressid = personemailaddressassoc.emailaddressid and isprimaryemailaddress = 1
			join person on personemailaddressassoc.personid = person.id
			join students on person.id = students.person\_id
			where students.id = :curStudId
			order by personemailaddressassoc.emailaddresspriorityorder asc)
			where rownum = 1
      \]\]></sql>
    </query>
    <query name="com.powerschool.core.dats.students.racecodes"
           x-ps-version="22.9.0"
           flattened="true"
		   coreTable="Students"
		   dat="true">
        <summary>DAT - Student Race Codes</summary>
        <description>Get the Students Race Codes for PowerQuery DAT.</description>
        <args>
            <arg name="curStudId" column="Students.ID" required="true" />
			<arg name="listaggdelim" column="Students.DCID" required="true" />
        </args>
        <columns>
            <column column="studentrace.RACECD">racecodes</column>
        </columns>
        <sql><!\[CDATA\[
			select LISTAGG(RACECD, :listaggdelim) racecodes
			from (select RACECD
				from studentrace
				join students on studentrace.studentid = students.id and students.id = :curStudId
				order by RACECD)
      \]\]></sql>
    </query>
    <query name="com.powerschool.core.dats.students.globalid"
           x-ps-version="22.9.0"
           flattened="true"
		   coreTable="Students"
		   dat="true">
        <summary>DAT - Student GlobalID</summary>
        <description>Get the Students GlobalID for PowerQuery DAT.</description>
        <args>
            <arg name="curStudId" column="Students.ID" required="true" />
        </args>
        <columns>
            <column column="pcas\_externalaccountmap.openiduseraccountid">openiduseraccountid</column>
        </columns>
        <sql><!\[CDATA\[
			select pcas\_externalaccountmap.openiduseraccountid
				from students
				join accessstudent on students.dcid = accessstudent.studentsdcid and students.id = :curStudId
				join pcas\_externalaccountmap on accessstudent.accountidentifier = pcas\_externalaccountmap.pcas\_accounttoken
					and pcas\_externalaccountmap.applicationusertype = 'STUDENT'
      \]\]></sql>
    </query>
    <query name="com.powerschool.core.dats.teachers.teacherglobalid"
           x-ps-version="22.9.0"
           flattened="true"
		   coreTable="SchoolStaff"
		   dat="true">
        <summary>DAT - Teacher GlobalID</summary>
        <description>Get the Teacher GlobalID for PowerQuery DAT.</description>
        <args>
            <arg name="curTchrId" column="SchoolStaff.ID" required="true" />
        </args>
        <columns>
            <column column="pcas\_externalaccountmap.openiduseraccountid">openiduseraccountid</column>
        </columns>
        <sql><!\[CDATA\[
			select distinct pcas\_externalaccountmap.openiduseraccountid
				from schoolstaff
				join accessteacher on schoolstaff.users\_dcid = accessteacher.teachersdcid and schoolstaff.id = :curTchrId
				join pcas\_externalaccountmap on accessteacher.accountidentifier = pcas\_externalaccountmap.pcas\_accounttoken
					and pcas\_externalaccountmap.applicationusertype = 'TEACHER'
      \]\]></sql>
    </query>
    <query name="com.powerschool.core.dats.teachers.adminglobalid"
           x-ps-version="22.9.0"
           flattened="true"
		   coreTable="SchoolStaff"
		   dat="true">
        <summary>DAT - Admin GlobalID</summary>
        <description>Get the Admin GlobalID for PowerQuery DAT.</description>
        <args>
            <arg name="curTchrId" column="SchoolStaff.ID" required="true" />
        </args>
        <columns>
            <column column="pcas\_externalaccountmap.openiduseraccountid">openiduseraccountid</column>
        </columns>
        <sql><!\[CDATA\[
			select distinct pcas\_externalaccountmap.openiduseraccountid
				from schoolstaff
				join accessadmin on schoolstaff.users\_dcid = accessadmin.teachersdcid and schoolstaff.id = :curTchrId
				join pcas\_externalaccountmap on accessadmin.accountidentifier = pcas\_externalaccountmap.pcas\_accounttoken
					and pcas\_externalaccountmap.applicationusertype = 'STAFF'
      \]\]></sql>
    </query>
    <query name="com.powerschool.core.dats.teachers.racecodes"
           x-ps-version="22.9.0"
           flattened="true"
		   coreTable="SchoolStaff"
		   dat="true">
        <summary>DAT - Teacher Race Codes</summary>
        <description>Get the Teacher Race Codes for PowerQuery DAT.</description>
        <args>
            <arg name="curTchrId" column="SchoolStaff.ID" required="true" />
			<arg name="listaggdelim" column="SchoolStaff.DCID" required="true" />
        </args>
        <columns>
            <column column="teacherrace.RACECD">racecodes</column>
        </columns>
        <sql><!\[CDATA\[
			select LISTAGG(RACECD, :listaggdelim) racecodes
			from (select distinct RACECD
				from teacherrace
				join schoolstaff on teacherrace.teacherid = schoolstaff.id and schoolstaff.id = :curTchrId
				order by RACECD)
      \]\]></sql>
    </query>

    <query name="com.powerschool.core.dats.students.late\_assignment\_counts\_year"
           x-ps-version="22.9.0"
           flattened="true"
		   coreTable="Students"
		   dat="true">
        <summary>DAT - Late Assignments - Year</summary>
        <description>Get the Students late assignment counts for the students active classes for the year.</description>
        <args>
            <arg name="curStudId" column="Students.ID" required="true" />
			<arg name="curyearid" column="Sections.TermID" required="true" />
        </args>
        <columns>
			<column column="courses.course\_name" codemap="CourseName" map-id="courses.course\_number">course\_name</column>
			<column column="courses.course\_number">course\_number</column>
            <column column="assignmentscore.assignmentsectionid">lateAsmtCount</column>
        </columns>
        <sql><!\[CDATA\[
					select
						courses.course\_name,
						courses.course\_number,
						count(\*) lateAsmtCount
						from students
						join cc on students.id = cc.studentid and students.id = :curStudId
						join sections on cc.sectionid = sections.id and sections.termid between (:curyearid \* 100) and ((:curyearid \* 100) + 99)
						join assignmentsection on sections.dcid = assignmentsection.sectionsdcid
						join assignmentscore on assignmentscore.assignmentsectionid = assignmentsection.assignmentsectionid 
							and students.dcid = assignmentscore.studentsdcid and assignmentscore.islate = 1
						join courses on lower(sections.course\_number) = lower(courses.course\_number)
						group by courses.course\_name, courses.course\_number
						having count(\*) > 0
						order by lower(courses.course\_name)
      \]\]></sql>
    </query>
	<query name="com.powerschool.core.dats.students.missing\_assignment\_counts\_year"
           x-ps-version="22.9.0"
           flattened="true"
		   coreTable="Students"
		   dat="true">
        <summary>DAT - Missing Assignments - Year</summary>
        <description>Get the Students missing assignment counts for the students active classes for the year.</description>
        <args>
            <arg name="curStudId" column="Students.ID" required="true" />
			<arg name="curyearid" column="Sections.TermID" required="true" />
        </args>
        <columns>
			<column column="courses.course\_name" codemap="CourseName" map-id="courses.course\_number">course\_name</column>
			<column column="courses.course\_number">course\_number</column>
            <column column="assignmentscore.assignmentsectionid">missingAsmtCount</column>
        </columns>
        <sql><!\[CDATA\[
					select
						courses.course\_name,
						courses.course\_number,
						count(\*) missingAsmtCount
						from students
						join cc on students.id = cc.studentid and students.id = :curStudId
						join sections on cc.sectionid = sections.id and sections.termid between (:curyearid \* 100) and ((:curyearid \* 100) + 99)
						join assignmentsection on sections.dcid = assignmentsection.sectionsdcid
						join assignmentscore on assignmentscore.assignmentsectionid = assignmentsection.assignmentsectionid 
							and students.dcid = assignmentscore.studentsdcid and assignmentscore.ismissing = 1
						join courses on lower(sections.course\_number) = lower(courses.course\_number)
						group by courses.course\_name, courses.course\_number
						having count(\*) > 0
						order by lower(courses.course\_name)
      \]\]></sql>
    </query>
	
</queries>

-   PS SIS API
    -   [Security](#/page/security)
    -   [Data Access](#/page/data-access)
    -   [Resources](#/page/resources)
    -   [Events](#/page/events)
    -   [Single Sign On](#/page/single-sign-on-2)
    -   [Examples](#/page/examples)
-   PS SIS Customization
    -   [Page Customization and Database Extensions](#/page/page-customization-and-database-extensions)
    -   [PowerQuery DAT](#/page/powerquery-dat)
    -   [PowerTeacher Pro Customization](#/page/powerteacher-pro-customization)
    -   [PS-HTML tags](#/page/ps-html-tags)
    -   [Plugins](#/page/plugins)
    -   [Student Contacts Customization](#/page/student-contacts-customization)
    -   [New Experience Start Page Customization](#/page/new-experience-start-page-customization)
    -   [PowerSchool SIS Attendance in Schoology Customizations](#/page/powerschool-sis-attendance-in-schoology-customizations)
    -   [Enhanced Navigation Customization](#/page/user-interface-customization)
-   Community
    -   [Blog](#/page/blog)
    -   [Developer Forums](https://support.powerschool.com/forum/319)
    -   [Video Library](#/page/video-library)
# Developer Admin

Toggle navigation [PowerSource](/)

# PS-HTML tags

"PS-HTML" or "PowerSchool HTML" is the term given to the language used to write traditional PowerSchool pages. It consists of HTML with special PowerSchool tags interspersed. These tags usually have one of the formats ~(tag) or ~\[tag\]. They essentially perform server-side operations that can modify the page results being sent out to the user's browser.

There are many, many PS-HTML tags supported by PowerSchool. This page documents some of the most useful ones.

## System Tags

Tag

Available

Description/Example

~(curstudid)

Admin  
Teachers  
Guardian  
Student  
Subs

Displays the students id of the record that was referenced on page load.  
Example: 1234

~(curtchrid)

Admin  
Teachers

Displays the teacher id of the record that was referenced on page load.  
Example: 4567

~(curschoolid)

Admin  
Teachers  
Guardian  
Student  
Subs

Displays the school id of the currently selected school.  
Output: School Number, 0 (District), or 999999 (Graduated Students)

~(curtermid)

Admin  
Teachers  
Guardian  
Student  
Subs

Displays the term id of the currently selected term.  
Example: 2401

~(curyearid)

Admin  
Teachers  
Guardian  
Student  
Subs

Displays the year id of the currently selected year.  
Example: 1234

~\[x:userid\]

Admin  
Teachers  
Guardian  
Student  
Subs

Displays the ID of the currently logged in user. For Guardian the current student ID is returned.  
Example: 1234

~\[x:userid;guardianid\]

Guardian

**PS 9.0+ Only**: Displays the GUARDIAN.GUARDIANID of the currently logged in user.  
Example: 1234

~\[x:users\_dcid\]

Admin  
Teachers

Displays the users dcid (Teachers.Users\_DCID) of the currently logged in Teacher/Administrator.  
Example: 1234

~\[x:usersroles\]

Admin  
Teachers

Displays the group number, or group numbers if using multiple roles, of the currently logged in Teacher/Administrator in a comma separated list.  
Example: 9 or 9,10,30

~\[x:username\]

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the current users name.  
Example: Andle, Brian  
  
Optional modifier ;firstlast

~\[eaodate\]

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the current date.  
Example: 2/2/2016

~\[date\]

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the current date.  
Example: 02/02/2016

~\[time\]

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the current time.  
Example: 10:10 AM

~\[x:version\]

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the current PS version number.  
Pre 10 this renders 3 places. Example: 9.2.4  
Post 10 this renders 2 places. Example: 9.2

~\[x:version;short\] (10+)

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the current PS version number.  
Example: 9.2.4

~\[x:version;long\] (10+)

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the current PS version number.  
Example: 9.2.4.0.1040000

~\[x:version;full\] (10+)

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the full PS version number for beta/internal builds.  
Example: 9.2.4.0.1040000.2123

~\[x:getbase\_termid\]  
~\[x:getbase\_termid;schoolid\]  
(19.11.0+)

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the base termid of the current school, or for the schoolid passed as an optional parameter.

~\[x:getbase\_termname\]  
~\[x:getbase\_termname;schoolid\]  
(19.11.0+)

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the base term name of the current school, or for the schoolid passed as an optional parameter.

~\[x:getbase\_termid\_public\]  
~\[x:getbase\_termid\_public;schoolid\]  
(19.11.0+)

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the base termid of the current school public portal, or for the schoolid passed as an optional parameter.

~\[x:getbase\_termname\_public\]  
~\[x:getbase\_termname\_public;schoolid\]  
(19.11.0+)

Admin  
Teachers  
Guardian  
Student  
Subs  

Displays the base term name of the current school public portal, or for the schoolid passed as an optional parameter.

~\[displaypref:{pref\_name};{default\_optional}\]

Admin  
Teachers  
Guardian  
Student  
Subs  

SQL Equivalent: select value from prefs where lower(name) = lower('{pref\_name}')

~\[displayprefschool:{pref\_name}\]

Admin  
Teachers  
Guardian  
Student  
Subs  

SQL Equivalent: select value from prefs where lower(name) = lower('{pref\_name}-S~(curschoolid)')

~\[displayprefyearschool:{pref\_name}\]

Admin  
Teachers  
Guardian  
Student  
Subs  

SQL Equivalent: select value from prefs where schoolid = ~(curschoolid) and yearid = ~(curyearid) and lower(name) = lower('{pref\_name}')

~\[displayprefschoolid:{pref\_name}\]

Admin  
Teachers  
Guardian  
Student  
Subs  

SQL Equivalent: select value from prefs where schoolid = ~(curschoolid) and lower(name) = lower('{pref\_name}')

~\[displayprefyear:{pref\_name}\]

Admin  
Teachers  
Guardian  
Student  
Subs  

SQL Equivalent: select value from prefs where yearid = ~(curyearid) and lower(name) = lower('{pref\_name}')

## Field DATs

DATs (Data Access Tags) are special tags designed to return a specific data element in current processing. A typical use is to return a column value from the table that the current page is processing, but there are other types of DATs also. Here are the formats for specific database field display DATs.

Tag

Field Type

Description/Example

~(\[{Table\_Name}\]{Field\_Name})

Real  
Legacy  
Custom

Displays the {Field\_Name} value for the current {Table\_Name}.  
Example: ~(\[Students\]lastfirst)  
Output: Jones, Tom

~(\[{Table\_Name}.{Database\_Extension}\]{Field\_Name})

Database Extension

Displays the {Field\_Name} value for the current {Table\_Name}'s associated {Database\_Extension}.  
Example: ~(\[Students.StudentCoreFields\]guardian)  
Output: Tom Jones

## IF Statements

Each of these statements has the following form:

~\[if.CONDITION\]
True Content
\[else\]
False Content
\[/if\]

The line breaks are optional and do not affect the result (except that line breaks will be in the result also). Please note that PS-HTML tags do not nest normally; if you want to nest one "if" inside another, you need to use the following type of construct:

~\[if#cond1.CONDITION1\]
  ~\[if#cond2.CONDITION2\]
    True Content for both conditions
  \[else#cond2\]
    False Content for condition 2 (only!)
  \[/if#cond2\]
\[else#cond1\]
  False Content for condition 1 only
\[/if#cond1\]

Conditions must be a simple conditional (i.e. "AND", "OR", or other Boolean operations are not allowed). Among the most useful of the available conditions are the following:

CONDITION

Available

Description/Example

{value}={testvalue}

Admin  
Teachers  
Guardian  
Subs

Tests if {value} equals {testvalue}.  
Example:  
~\[if.1=1\] It Works \[/if\]  
Available Operators: =, #, >, >=, <, <=, $ (Contains), !$ (Does not contain)

"{value}" in ("{testvalue1}","{testvalue2}")

Admin  
Teachers  
Guardian  
Subs

PS SIS 22.12+  
Tests if {value} equals {testvalue1} _or_ {testvalue2}.  
Example:  
~\[if."1" in ("1","2")\] It Works \[/if\]  
Available Operators: in, not in

gpv.{postedname}={testvalue}

Admin  
Teachers  
Guardian  
Subs

Tests if the posted value equals {testvalue}.  
Example:  
~\[if.gpv.my\_gpv=1\] It Works \[/if\]  
Available Operators: =

pref.{prefname}={testvalue}

Admin  
Teachers  
Guardian  
Subs

Tests if the preference value equals {testvalue}.  
Example:  
~\[if.pref.my\_pref=1\] It Works \[/if\]  
Available Operators: =

prefschool.{prefname}={testvalue}

Admin  
Teachers  
Guardian  
Subs

Tests if the school preference value equals {testvalue}.  
Example:  
~\[if.prefschool.my\_school\_pref=1\] It Works \[/if\]  
Available Operators: =

security.inrole={group\_number}

Admin  
Teachers

Tests if a user belongs to at least one of the defined groups.  
Example:  
~\[if.security.inrole=1,2,3,4,9,20\]  
True if part of one of these security groups/roles (1,2,3,4,9,20)  
\[/if\]

security.pageviewormod={path/page}

Admin

PS SIS 24.5.0.0+  
Tests if the currently logged in user has view or modify access to the page.  
Example:  
~\[if.security.pageviewormod=/admin/students/generaldemographics.html\]  
<a href="generaldemographics.html?frn=~(studentfrn)">Demographics</a>  
\[/if\]

security.pagemod={path/page}

Admin

Tests if the currently logged in user has modify access to the page.  
Example:  
~\[if.security.pagemod=/admin/students/generaldemographics.html\]  
<a href="generaldemographics.html?frn=~(studentfrn)">Demographics</a>  
\[/if\]

security.pageview={path/page}

Admin

Tests if the currently logged in user has view access to the page.  
Example:  
~\[if.security.pageview=/admin/students/generaldemographics.html\]  
<a href="generaldemographics.html?frn=~(studentfrn)">Demographics</a>  
\[/if\]

security.pagenone={path/page}

Admin

Tests if the currently logged in user has no access to the page.  
Example:  
~\[if.security.pagenone=/admin/students/generaldemographics.html\]  
No Access  
\[else\]  
<a href="generaldemographics.html?frn=~(studentfrn)">Demographics</a>  
\[/if\]

security.canmodifyfield={Table.Field}

Admin

Tests if the currently logged in user has modify access to the Table.Field.  
Example:  
~\[if.security.canmodifyfield=Students.DOB\]  
Full Access  
\[/if\]

security.canviewfield={Table.Field}

Admin

Tests if the currently logged in user has view access to the Table.Field.  
Example:  
~\[if.security.canviewfield=Students.DOB\]  
View Acess or Possibly Modify  
\[/if\]

security.noaccessfield={Table.Field}

Admin

Tests if the currently logged in user has no access to the Table.Field.  
Example:  
~\[if.security.noaccessfield=Students.DOB\]  
No Access  
\[/if\]

security.fieldlevel={Table.Field}{Operator}{Access\_level}

Admin

Tests if the currently logged in user has no access to the Table.Field based on access level.  
Example:  
~\[if.security.fieldLevel=Students.Dob>NoAccess\]  
Has view or modify access  
\[/if\]  
Available Operators: >=, <=, !=, >, <, =  
Available Access\_level: NoAccess, ViewOnly, FullAccess

is\_prod

Admin  
Teachers  
Guardian  
Subs

PS SIS 19.11+  
Tests if the server is configured as a Production or Test server.  
Example:  
~\[if.is\_prod\]  
Is production server  
\[/if\]

## GPVs (Get Post Value)

The Get Post Value (gpv) tags get values sent in to the request as either Query String or POST parameters. There are two formats for these tags, as seen in various areas of the documentation:

~(gpv.{posted\_value\_name})

~\[gpv:{posted\_value\_name}\]

**DO NOT USE!**

The two do basically the same thing, but are processed by PowerSchool in different ways. You should always use the round bracket version. (Among other things, the square bracket version does not have all the options below.)

Tag

Available

Description/Example

~(gpv.{posted\_value\_name};encodejsstring)

Admin  
Teachers  
Guardian  
Subs

Returns the posted value from the URL or the previous page submit and returns the JavaScript encoded result.  
URL Request: /admin/home.html?my\_gpv=it's >< alive  
GPV Tag: ~(gpv.my\_gpv;encodejsstring)  
Result: it\\'s >< alive  
Where to use: When the gpv returned is to be used in JavaScript string.

~(gpv.{posted\_value\_name};encodejsonstring)

Admin  
Teachers  
Guardian  
Subs

Returns the posted value from the URL or the previous page submit and returns the JSON encoded result.  
URL Request: /admin/home.html?my\_gpv=it's >< alive  
GPV Tag: ~(gpv.my\_gpv;encodejsonstring)  
Result: it\\'s >< alive  
Where to use: When the gpv returned is to be used in JavaScript string.

~(gpv.{posted\_value\_name};urlencode)

Admin  
Teachers  
Guardian  
Subs

Returns the posted value from the URL or the previous page submit and returns the url encoded result.  
URL Request: /admin/home.html?my\_gpv=it's >< alive  
GPV Tag: ~(gpv.my\_gpv;urlencode)  
Result: it%27s+%3E%3C+alive  
Where to use: When the gpv returned is to be used as part of a link.

~(gpv.{posted\_value\_name};encodehtml)

Admin  
Teachers  
Guardian  
Subs

Returns the posted value from the URL or the previous page submit and returns the html encoded result.  
URL Request: /admin/home.html?my\_gpv=it's >< alive  
GPV Tag: ~(gpv.my\_gpv;encodehtml)  
Result: it's >< alive  
Where to use: When the gpv returned is displayed in a webpage.

~(gpv.{posted\_value\_name};num)

Admin  
Teachers  
Guardian  
Subs

Returns the posted numeric value from the URL or the previous page submit.  
URL Request: /admin/home.html?my\_gpv=it's >< alive  
GPV Tag: ~(gpv.my\_gpv;num)  
Result: 0  
URL Request: /admin/home.html?my\_gpv=43  
GPV Tag: ~(gpv.my\_gpv;num)  
Result: 43  
Where to use: When the gpv returned is to be guaranteed or supposed to be a number.

~(gpv.{posted\_value\_name};sqlText)

Admin  
Teachers  
Guardian  
Subs

Returns the posted value from the URL or the previous page submit and returns the sql encoded result.  
URL Request: /admin/home.html?my\_gpv=it's >< alive  
GPV Tag: ~(gpv.my\_gpv;sqlText)  
Result: it''s >< alive  
Where to use: When the gpv returned is to be used in a tlist\_sql tag

~(gpv.{posted\_value\_name};if.blank.then={some\_default\_value})

Admin  
Teachers  
Guardian  
Subs

Returns the posted value from the URL or the previous page submit and returns a default value if the value is blank.  
URL Request: /admin/home.html?my\_gpv=  
GPV Tag: ~(gpv.my\_gpv;if.blank.then=-1)  
Result: -1  
Where to use: When the gpv returned will possibly be blank  
  
The modifiers on GPV's can be combined to ensure a value it returned and formatted correctly. For example:  
GPV Tag: ~(gpv.my\_gpv;if.blank.then=-1;sqlText)

~(gpv.{posted\_value\_name};onlynumeric)  
(21.4.2+)

Admin  
Teachers  
Guardian  
Subs

Removes any characters that are not in the following list:  
+-.,0123456789e

~(gpv.{posted\_value\_name};onlyalpha)  
(21.4.2+)

Admin  
Teachers  
Guardian  
Subs

Removes any characters that are not a-Z.

~(gpv.{posted\_value\_name};onlyalphanumeric)  
(21.4.2+)

Admin  
Teachers  
Guardian  
Subs

Removes any characters that are not in the following list:  
+-.,0123456789e or a-Z

~(gpv.{posted\_value\_name};onlydatecharacters)  
(21.4.2+)

Admin  
Teachers  
Guardian  
Subs

Removes any characters that are not in the following list:  
,-./0123456789

## TLIST\_SQL

"TLIST\_SQL" is the name of a tag that executes SQL right in the page and returns the results. This function is considered _**STRONGLY DEPRECATED**_ in new development work for several reasons. If not used carefully the use of TLIST\_SQL can easily introduce security issues. In place of TLIST\_SQL, the use of [PowerQueries](#/page/powerqueries) is recommended. The following are some examples of potential exposure points when using TLIST\_SQL:

-   Field Level Security (FLS) is not applied by default which _could_ result in accessing Fields the user does not have permission to access.
-   Data Restriction Framework (DRF) is not available which _could_ result in users accessing Rows in the database the user does not have permission to access.
-   Improper, or lack of proper escaping of DATs, such as GPV tags, _could_ result in SQL Injection. This could result users being able to bypass security protections that have been added to the TLIST\_SQL's SQL.

The following table is a reference of the modifiers that can be applied to variables in the tlist\_sql statement.

Modifier

Description/Example

;d

Date - Formats a column that's a DATE or TIMESTAMP type to display in currently logged in users locale format.  
Example:  
Tag: ~\[tlist\_sql;select sysdate from dual\]~(sysdate;d)\[/tlist\_sql\]  
Output: 2/2/2015

;l;format=time

Time - Converts a column that's a NUMBER that represents seconds as HH:MM.  
Example  
Tag: ~\[tlist\_sql;select start\_time, end\_time from bell\_schedule\_items\]~(start\_time;l;format=time) -- ~(end\_time;l;format=time)\[/tlist\_sql\]  
Output: 9:34 AM -- 10:34 AM

;url

URL escapes text to be used as part of a link  
Example  
Tag: ~\[tlist\_sql;select 'hello world' myLink from dual\]<a href="/admin/test.html?mygpv=~(myLink;url)">My Link</a>\[/tlist\_sql\]  
Output: <a href="/admin/test.html?mygpv=hello+world">My Link</a>

;Js

JS will escape specific characters so that the returned value in the TList\_SQL can be injected into a Javascript String value.  
As of 8.3 this was modified to perform true JavaScript escaping.  
Example  
Tag: ~\[tlist\_sql;select 'Every "High" ''School''' schoolName from dual\]~(schoolName;js)\[/tlist\_sql\]  
Output: Every \\"High\\" \\'School\\'

;Json (9.0+)

Json will escape specific characters so that the returned value in the TList\_SQL can be injected into a JSON object.  
Example  
Tag: ~\[tlist\_sql;select 'Every "High" ''School''' schoolName from dual\]~(schoolName;json)\[/tlist\_sql\]  
Output: Every \\"High\\" 'School'

;Html (9.0+)

Html will escape specific characters so that the returned value in the TList\_SQL can be rendered to the page while preventing html injection.  
Example:  
Tag: ~\[tlist\_sql;select '< >' gtLt from dual\]~(gtLt;html)\[/tlist\_sql\]  
Output: < >

;Xml10 (9.0+)

Xml10 will escape specific characters so that the returned value in the TList\_SQL can be injected into a XML v1.0 object.  
Example:  
Tag: ~\[tlist\_sql;select '< > ''' gtLt from dual\]~(gtLt;xml10)\[/tlist\_sql\]  
Output: < > &apos;

;Xml11 (9.0+)

Xml11 will escape specific characters so that the returned value in the TList\_SQL can be injected into a XML v1.1 object.  
Example:  
Tag: ~\[tlist\_sql;select '< > ''' gtLt from dual\]~(gtLt;xml10)\[/tlist\_sql\]  
Output: < > &apos;

;ReplaceCRLFWithBR (20.11.0.1+)

ReplaceCRLFWithBR will replace CR, LF, CRLF with <br/>  
Example:  
Tag: ~\[tlist\_sql;select chr(13)||chr(10) gtLt from dual\]~(gtLt;ReplaceCRLFWithBR)\[/tlist\_sql\]  
Output: <br/>

-   PS SIS API
    -   [Security](#/page/security)
    -   [Data Access](#/page/data-access)
    -   [Resources](#/page/resources)
    -   [Events](#/page/events)
    -   [Single Sign On](#/page/single-sign-on-2)
    -   [Examples](#/page/examples)
-   PS SIS Customization
    -   [Page Customization and Database Extensions](#/page/page-customization-and-database-extensions)
    -   [PowerQuery DAT](#/page/powerquery-dat)
    -   [PowerTeacher Pro Customization](#/page/powerteacher-pro-customization)
    -   [PS-HTML tags](#/page/ps-html-tags)
    -   [Plugins](#/page/plugins)
    -   [Student Contacts Customization](#/page/student-contacts-customization)
    -   [New Experience Start Page Customization](#/page/new-experience-start-page-customization)
    -   [PowerSchool SIS Attendance in Schoology Customizations](#/page/powerschool-sis-attendance-in-schoology-customizations)
    -   [Enhanced Navigation Customization](#/page/user-interface-customization)
-   Community
    -   [Blog](#/page/blog)
    -   [Developer Forums](https://support.powerschool.com/forum/319)
    -   [Video Library](#/page/video-library)
# Advanced User Guide for Database Extensions

#### PowerSchoo[pdfRest Free Demo]on Systems

**Updated: April 30, 2019**

Document Owner: Documentation Services

This edition applies to Release 12.x of the Po[pdfRest Free Demo]and to all subsequent releases and modifications until other[pdfRest Free Demo]ew editions or updates.

The data and names used to illustrate[pdfRest Free Demo]reen images may include names of individuals, companies, bra[pdfRest Free Demo]All of the data and names are fictitious; any similarities t[pdfRest Free Demo]entirely coincidental.

PowerSchool is a trademark, in the U.S[pdfRest Free Demo]tries, of PowerSchool Group LLC or its affiliate(s).

Copyright © 2005-2019 PowerSchool Group LLC and/or its affiliate(s). [pdfRest Free Demo].

All trademarks are either owned or licensed by PowerSchool [pdfRest Free Demo]s affiliates.

**PowerSchool.com**

**Contents**

**Introduction .[pdfRest Free Demo]............................................................[pdfRest Free Demo]................................... 2**

**Page Customization [pdfRest Free Demo]ions .......................................................[pdfRest Free Demo]........... 3**

[One-To-One Table Extensions ................[pdfRest Free Demo]............................................................................ 3](page1.html#linkTarget2)

[Sample Code: Vari[pdfRest Free Demo]sing One-To-One Extensions ......................................... 3](page1.html#linkTarget3)

[One-To-Many Table Extensi[pdfRest Free Demo]............................................................[pdfRest Free Demo]............... 4](page1.html#linkTarget4)

[tlist\_child ....[pdfRest Free Demo]............................................................[pdfRest Free Demo]................................. 4](page1.html#linkTarget5)

[Independent Table Extensions ...............................[pdfRest Free Demo]............................................................ 5](page1.html#linkTarget6)

[tlist\_standalone ...............[pdfRest Free Demo]............................................................[pdfRest Free Demo]............ 5](page1.html#linkTarget7)

[Special Formatting of tlist\_child and tlist\_standalone Columns ................[pdfRest Free Demo]........................ 6](page1.html#linkTarget8)

[Drop-Dow[pdfRest Free Demo]............................................................[pdfRest Free Demo]................ 7](page1.html#linkTarget9)

[Drop-Down Menu Example (Value/Name Pair) ....................................[pdfRest Free Demo]............. 8](page1.html#linkTarget10)

[Radio Button Examp[pdfRest Free Demo]............................................................[pdfRest Free Demo]............ 9](page1.html#linkTarget11)

[Radio Button Example (Value/Name Pair) .........................................[pdfRest Free Demo]................ 9](page1.html#linkTarget12)

[Text Area Examp[pdfRest Free Demo]............................................................[pdfRest Free Demo]................ 10](page1.html#linkTarget13)

[Static/Read On[pdfRest Free Demo]............................................................[pdfRest Free Demo]......... 11](page1.html#linkTarget14)

[Static/Read Only Afte[pdfRest Free Demo]le ..................................................................... 11](page1.html#linkTarget15)

[Multiple Special Fo[pdfRest Free Demo]le .........................................................[pdfRest Free Demo]](page1.html#linkTarget16)

[Using CSS Styles to Resize Input [pdfRest Free Demo]................................................................... 12](page1.html#linkTarget17)

[Using A Proper File Record Number (FRN) For Staff ...................................[pdfRest Free Demo]............. 13](page1.html#linkTarget18)

[Direct Access to [pdfRest Free Demo]Tables .....................................................[pdfRest Free Demo]............... 15](page1.html#linkTarget19)

[DirectTable.Sel[pdfRest Free Demo]............................................................[pdfRest Free Demo].......................... 15](page1.html#linkTarget20)

[Dire[pdfRest Free Demo]~(gpv) .....................................................[pdfRest Free Demo]..................... 15](page1.html#linkTarget21)

[Where To [pdfRest Free Demo]le.Select Tag ..............................................[pdfRest Free Demo]....... 16](page1.html#linkTarget22)

[Editing an Existing Rec[pdfRest Free Demo]............................................................[pdfRest Free Demo]............. 17](page1.html#linkTarget23)

[Adding a New Reco[pdfRest Free Demo]............................................................[pdfRest Free Demo]................... 17](page1.html#linkTarget24)

[Adding a New Record to a One-to-one Extension for a Specific Parent Reco[pdfRest Free Demo] 17](page1.html#linkTarget25)

[Adding a New Record to a One-to-One Extension and Fields in the Parent Table ............. 17](page1.html#linkTarget26)

[Adding a New Record to a One-to-[pdfRest Free Demo] Child Table ................................. 18](page1.html#linkTarget27)

[Adding a New Record to a One-to-one Extension[pdfRest Free Demo]................................... 18](page1.html#linkTarget28)

[Deleting a Record ......................................[pdfRest Free Demo]............................................................... 19](page1.html#linkTarget29)

**Custom Insertion Points and[pdfRest Free Demo]............................................................[pdfRest Free Demo]. 20**

[How it Works ........................................[pdfRest Free Demo]............................................................[pdfRest Free Demo]](page1.html#linkTarget31)

[Page Fragments ..................[pdfRest Free Demo]............................................................[pdfRest Free Demo]............... 20](page1.html#linkTarget32)

[Standard Insert[pdfRest Free Demo]............................................................[pdfRest Free Demo]..................... 21](page1.html#linkTarget33)

[Special C[pdfRest Free Demo]............................................................[pdfRest Free Demo].......................... 21](page1.html#linkTarget34)

[Spec[pdfRest Free Demo]s ..........................................................[pdfRest Free Demo].................................. 21](page1.html#linkTarget35)

[Auto-Insertions: How to Use Defined Insertion Points ....[pdfRest Free Demo]........................................ 22](page1.html#linkTarget36)

[URL-Based Auto-Insertion of Page Fragments ........[pdfRest Free Demo]................................................. 22](page1.html#linkTarget37)

[Wildcard-Based Auto-Insertion of Page Frag[pdfRest Free Demo]....................................................... 24](page1.html#linkTarget38)

[Moving Inserted Page Fragments To An[pdfRest Free Demo]he Target Page ............................. 24](page1.html#linkTarget39)

[XML-Based Movement of Page Fragments ..........[pdfRest Free Demo].................................................... 25](page1.html#linkTarget40)

[jQuery-Based Movement of Page Fragments[pdfRest Free Demo]........................................................... 26](page1.html#linkTarget41)

**Upload Custom Web Page Files ..[pdfRest Free Demo]............................................................[pdfRest Free Demo].... 28**

**Plugin Packages .................................[pdfRest Free Demo]........................................................................... 28**

[ZIP File Format ........................[pdfRest Free Demo]............................................................[pdfRest Free Demo]... 28](page1.html#linkTarget44)

[MessageKey Properties File [pdfRest Free Demo]............................................................[pdfRest Free Demo]. 28](page1.html#linkTarget45)

[Plugin Package Example Layout[pdfRest Free Demo]............................................................[pdfRest Free Demo]. 29](page1.html#linkTarget46)

[Creating a Plugin Package ...[pdfRest Free Demo]............................................................[pdfRest Free Demo]...... 30](page1.html#linkTarget47)

[Installing a Plugin Pack[pdfRest Free Demo]............................................................[pdfRest Free Demo]............ 31](page1.html#linkTarget48)

[How to Import a Pl[pdfRest Free Demo]............................................................[pdfRest Free Demo].... 31](page1.html#linkTarget49)

[Important Information on P[pdfRest Free Demo]e/Disable/Delete ............................................ 32](page1.html#linkTarget50)

**Appendix A: List of Insertion[pdfRest Free Demo]............................................................[pdfRest Free Demo]........ 33**

**Appendix B: Direct Table Access Example Code [pdfRest Free Demo].............................................................. 35**

[Main Page: /admin/students/applications.html ........[pdfRest Free Demo]............................................. 35](page1.html#linkTarget53)

[Edit a Record: /admin/students/applications-ed[pdfRest Free Demo]................................................. 37](page1.html#linkTarget54)

[Add a New Record: /admin/students/applications-new.html ................................................... 39](page1.html#linkTarget55)

**Revision History**

**Release Date**

**Version**

**Changes**

November 2013

1.0

Initial release[pdfRest Free Demo]9

February 2014

1.1

- **•** Minor corrections to page fragment section

March 2014

1.2

- **•** Updated for new features in PowerSchool 7.11
  
  - **»** tlist\_child and tlist\_standalone new JSON output type
  - **»** XML-Based Movement of Page Fragments
  - **»** MessageKey files in Plugin Packages
- **•** New ap[pdfRest Free Demo]insertion points
- **•** Cleaned up form elements sample code
- **•** Using CSS Styles to Resize Input Fields
- **•** Sample[pdfRest Free Demo]ge fragment
- **•** jQuery-Based Movement of page fragments
- **•** Added screenshots for examples
- **•** Added more detail[pdfRest Free Demo]kages section

June 2014

1.3

- **•** Updated for new features in PowerSchool 8.0

**»**

Added new value/pair versions of the drop-down menu, radio button, and static/read only after submit examples

June 2015

1.4

- **•** Rebranding

<!--THE END-->

- **•** U[pdfRest Free Demo]ures in PowerSchool 9.0
- **•** New ~\[DirectTable.Select] ta[pdfRest Free Demo](Appendix B)
- **•** New plugin content
  
  - **o** PowerQuery
  - **o** Permission Mapping
- **•** Fixed incorrect field nam[pdfRest Free Demo] code examples for Special Formatting of tlist\_child and tlist\_standalone Columns
- **•** Added example screenshots

January 2019

1.5

- **•** Rebranding

**Introduction**

This guide is des[pdfRest Free Demo]users working with the Database Extension features originall[pdfRest Free Demo]School 7.9 and new features released thereafter.

For details [pdfRest Free Demo]ons, see the Database Extensions section of the PowerSchool online help.

**Page Customization and Database Extensions**

You[pdfRest Free Demo]zed pages using HTML that access one-to-one table extensions, one-to-many child tables, and stand-alone table data elemen[pdfRest Free Demo]terface elements (such as drop-down menus, radio buttons, checkboxes, etc.) as well as allow for conditional elements bas[pdfRest Free Demo]nsion data element values.

**One-To-One Table Extensions**

To work with a one-to-one table extension on a page in PowerScho[pdfRest Free Demo]rimary table, the database extension group name, and the fie[pdfRest Free Demo]owing format:

```
[PrimaryTable.ExtensionGroupName]Field_Name
```

The examples below show a variety of ways to reference a one-to-one extension to the Students table. For this example we[pdfRest Free Demo] extension to track loaned laptops. The extension group name[pdfRest Free Demo]is U\_Laptop.

**Sample Code: Various Form Elements Using One-To-One Extensions**

<!-- Entry Field -->  
<tr>  
<td class="bold">Model Number</td>  
<td>  
<input type="text" name="\[Students.U\_Laptop]Model\_Number" value="" size="15"> </td>  
</tr>

<!-- Static[pdfRest Free Demo]splay -->  
<tr>  
<td class="bold">Barcode # (Read Only)</td>  
<td>  
~(\[Students.U\_Laptop]Barcode)  
</td>  
</tr>

<!-- Static/Read Only Field Inside Input Box --> <tr>  
<td class="bold">Barcode # (Read Only)</td>  
<td>  
<input type="text" name="\[Students.U\_Laptop]Barcode" value="" readonly="readonly">  
</td>  
</tr>

<!-- Radio Button -->  
<tr>  
<td class="bold">Operating System</td>  
<td>  
<input type="radio" name="\[Students.U\_Laptop]OS" value="Windows">Windows <input type="radio" name="\[Students.U\_Laptop]OS" value="Mac">Mac </td>  
</tr>

<!-- Check Box -->  
<tr>  
<td class="bold">Laptop Lost?</td>  
<td>

<input[pdfRest Free Demo]me="\[Students.U\_Laptop]IsLost" value="1"> </td>  
</tr>

<!-- Drop Down/Popup Menu -->  
<tr>  
<td class="bold">Manufacturer</td>  
<td>  
<select name="\[Students.U\_Laptop]Manufacturer"> <option value="">Select a Company</option>

<option  
<option  
<option  
<option  
<option  
<option  
</select> </td>  
</tr> value="Acer">Acer</option> value="Alienware">Alienware</option> value="Apple">Apple</option> value="Asus">Asus</option> value="Compaq">Compaq</option> value="Dell">Dell</option>

<!-- Text Area -->  
<tr>  
<td class="bold">Damages Comments</td>  
<td>  
<textarea name="\[Students.U\_Laptop]Damages\_Comment[pdfRest Free Demo]5"> </textarea>  
</td>  
</tr>

**One-To-M[pdfRest Free Demo]s**

A one-to-many table extension creates a child table to th[pdfRest Free Demo] table and allows multiple records to be created that are ti[pdfRest Free Demo] parent record. Examples of existing one-to-many tables in P[pdfRest Free Demo]e Students table is the parent are Special Programs, Logs, a[pdfRest Free Demo]s. To view, store, and retrieve data from your own one-to-ma[pdfRest Free Demo]cial HTML tag called tlist\_child. This tag can auto-generat[pdfRest Free Demo]display rows of records from the designated child table, inc[pdfRest Free Demo]n and Delete button for each row you create.

**tlist\_child**

[pdfRest Free Demo]e format for the tlist\_child HTML tag.

~\[tlist\_child:<CoreTableName>.<ExtensionGroup>.<ExtensionTable>;displaycols:< List of Fields>;fieldNames:<List of Column Headers>;type:<FormatName>]

The following prov[pdfRest Free Demo]ormation on this tag:

- The **<CoreTableName>.<ExtensionGroup>.<ExtensionTable>** narrows the query down[pdfRest Free Demo]table. For example, a child table to track college applicati[pdfRest Free Demo]TS.U\_COLLEGEAPP.U\_APPLICATIONS
- The **displaycols** are a comma-separated list of fields from the one-to-many table, an[pdfRest Free Demo]r all of the defined fields in that table. Two special ID fi[pdfRest Free Demo]ferenced: ID and <CoreTableName>DCID. In the college e[pdfRest Free Demo]ould be STUDENTSDCID.
- The **fieldNames** are a comma-separa[pdfRest Free Demo]els that should appear in the auto-generated HTML table head[pdfRest Free Demo]ay contain spaces.

<!--THE END-->

- The **type** parameter spe[pdfRest Free Demo]lid format options are "html" or "json"

» html: Automatically[pdfRest Free Demo]able that allows for dynamic record creation and deletion.

»

json: The output of the tlist\_child will be a JSON array with[pdfRest Free Demo]"Results". It is not necessary to include fieldNames when us[pdfRest Free Demo] field in the array will always be the ID field.

Add the tlist\_child tag wherever you would like the table to appear on y[pdfRest Free Demo]wing is an example for a one-to-many table to track college applications:

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Re quest\_Date,Request\_Status[pdfRest Free Demo]tion\_Date,Outcome,Notes;fieldName s:Institution,Request Dat[pdfRest Free Demo]p?,Completion Date,Outcome,Notes; type:html]

Example screenshot of a table auto-generated by tlist\_child:

**Independent Table Extensions**

An independent table extension creates a tabl[pdfRest Free Demo]iated with any existing PowerSchool table. Examples of exist[pdfRest Free Demo]les in PowerSchool are States, CountryISOCodeLU, LocaleTimeF[pdfRest Free Demo]s. To view, store, and retrieve data from your own independe[pdfRest Free Demo]cial HTML tag called tlist\_standalone. This tag can auto-ge[pdfRest Free Demo]e to display your rows of records, including an Add button a[pdfRest Free Demo]or each row that has been created.

**tlist\_standalone**

The f[pdfRest Free Demo]mat for the tlist\_standalone HTML tag.

~\[tlist\_standalone:<ExtensionGroup>.<ExtensionTable>;displaycols:<List of Fields>;fieldNames:<List of Column Headers>;type:<FormatName>]

The following provides additional in[pdfRest Free Demo]ag:

- The **<ExtensionGroup>.<ExtensionTable>** n[pdfRest Free Demo]wn to a single independent table. For example, an independen[pdfRest Free Demo]maintain a master list of all higher education institutions could be U\_CollegeApp.U\_Institutions.
- The **displaycols** are a comma-separated list of fields from the standalone tabl[pdfRest Free Demo]any or all of the defined fields in that table. A database r[pdfRest Free Demo]also be referenced using the field name ID. This field is au[pdfRest Free Demo] when the table is initially defined.
- The **fieldNames** are a comma-separated list of the labels that should appear in the auto-generated HTML table heading. These labels may contain spaces.
- The **type** parameter specifies a format. Valid [pdfRest Free Demo]"html" or "json"

» html: Automatically generate an HTML table[pdfRest Free Demo]namic record creation and deletion.

»

json:

The output of the tlist\_standalone will be a JSON array with an object name of [pdfRest Free Demo]t necessary to include fieldNames when using JSON. The first[pdfRest Free Demo] will be the ID field.

Add the tlist\_standalone tag wherever[pdfRest Free Demo] table to appear on your page. The following is an example f[pdfRest Free Demo]of all higher education institutions:

~\[tlist\_standalone:U\_CollegeApp.U\_Institutions;displaycols:IPEDS\_ID,Instituti on\_Name,Institution\_Type,Phone,URL;fieldNames:IPEDS ID,Insti[pdfRest Free Demo]tion Type,Phone Number,Web Address;type:html]

**Special Formatting of tlist\_child and tlist\_standalone Columns**

By defau[pdfRest Free Demo]ns in an auto-generated HTML table will be input fields appr[pdfRest Free Demo]ters wide unless the extended field type is date or Boolean.[pdfRest Free Demo]e the pop-up calendar widget. Boolean fields are displayed a[pdfRest Free Demo]ial code and tags have been created which allow the remainin[pdfRest Free Demo]e changed to drop-down menus, radio buttons, text area or st[pdfRest Free Demo]ssible to modify the width of the input fields using Cascading Style Sheets.

**Important Note:** When using special format[pdfRest Free Demo]ield name used within the script must exactly match the fiel[pdfRest Free Demo]tlist\_child or tlist\_standalone tag. For example, if the tlist\_child displaycols lists the field name as “Institution” do not use “INSTITUTION” in the script code: tlistText2DropDown('INSTITUTION',InstValues).

Example screenshot of a table auto-generated by tlist\_child with special formatting applied:

**Drop-Down Menu Example**

- Within the <head> tag, add[pdfRest Free Demo]tion.js JavaScript file by including: <script src="/scrip[pdfRest Free Demo]on.js"></script>
- Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
- Directly after the tlist tag, use a script sim[pdfRest Free Demo]ng example for field(s) you want to change from input text to a drop-down menu. The script will define the drop-down menu[pdfRest Free Demo]d be displayed to users and stored in the database. It will [pdfRest Free Demo]iable name to be used to identify the list. Any variable nam[pdfRest Free Demo] script could be repeated if more than one field needs to be[pdfRest Free Demo]p-down menu. In this case unique variable names must be used[pdfRest Free Demo]e variable defines the value options, the following command [pdfRest Free Demo]t:

```
tlistText2DropDown('<FieldName>',<JavaScript_Variable_Name>);
```

<!DOCTYPE html>  
<html>  
<!-- start right frame -->  
<head>  
<title>College Applications</title>  
~\[wc:commonscripts]

<script src="/scr[pdfRest Free Demo]tion.js"></script> <link href="/images/css/scree[pdfRest Free Demo]eet" media="screen"> <link href="/images/css/print.css[pdfRest Free Demo]media="print"> </head>

<body>  
<form action[pdfRest Free Demo]orded.white.html" method="POST"> ~\[wc:admin\_header\_frame\_css]<!-- breadcrumb start --><a href="/admin/home.html" target="\_top">Start Page</a> > <a href="home.html?selectstudent=nosearch" target="\_top">Student Selection</a> > College Applications<!-- breadcrumb end -- >~\[wc:admin\_navigation\_frame\_css]

~\[wc:title\_student\_begin\_css]College Applications~\[wc:title\_student\_end\_css]

<!-- start of content and bounding box -->

<div class="box-round">  
~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Dat e,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Requ est Date,Request Status,Scholar[pdfRest Free Demo]e,Outcome,Notes;type:html]

<script>  
var InstValues = {};  
InstValues\['1']='Option 1';  
InstValues\['2']='Option 2';  
InstValues\['3']='Option 3';  
InstValues\['4']='Option 4';  
InstValues\['5']='Option 5';  
InstValues\['6']='Option 6';  
InstValues\['7']='Option 7';  
InstValues\['8']='Option 8';  
InstValues\['9']='Option 9';  
InstValues\['10']='Option 10';  
tlistText2DropDown('Institution',InstValues);

</script>

<br>

<div class="button-row">  
<input type="hid[pdfRest Free Demo]e="prim">~\[submitbutton] </div>  
</div>  
<br>  
<!-- end of content of bounding box -->  
~\[wc:admin\_footer\_frame\_css]

</form>

</body>  
</html><!-- end right frame -->

**Drop-Down Menu Example (Value/Name Pair)**

**Introduced in PowerSchool 8.0**

This version of the drop-down menu script differs from the original v[pdfRest Free Demo]control over both the value that is displayed in the drop-do[pdfRest Free Demo]tely, the value that is stored in the database.

- Within the <head> tag, add the tlistCustomization.js JavaScript file by including: <script src="/scripts/tlistCustomization.js"></script>
- Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
- Directly after the tlist tag, use a script similar[pdfRest Free Demo]xample for field(s) you want to change from input text to a drop-down menu. The script will define both the drop-down men[pdfRest Free Demo]ld be displayed to users and a chosen value to be stored in [pdfRest Free Demo]ll also define the variable name to be used to identify the [pdfRest Free Demo]name may be used. This script could be repeated if more than[pdfRest Free Demo] be displayed as a drop-down menu. In this case unique varia[pdfRest Free Demo]sed for each.

After the variable defines the value options, t[pdfRest Free Demo]d completes the script:

```
tlistText2DropDownValNamePair
('<FieldName>',<JavaScript_Variable_Name >);
```

**Note:** The follo[pdfRest Free Demo]de as the first drop-down example, but only the tlist\_child[pdfRest Free Demo]wn.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Dat e,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Req[pdfRest Free Demo]Status,Scholarship,Completion Date,Outcome,Notes;type:html]

<script>  
var InstValues = \[];  
InstValues.push(\['C','Considering']);  
InstValues.push(\['W','Waitlist']);  
InstValues.push(\['A','Accepted']);  
InstValues.push(\['D','Denied']);  
InstValues.push(\['5','Option 5']);  
tlistText2DropDownValNamePair('Outcome',InstValues); </script>

**Radio Button Example**

- Within the <head> tag, add the tlistCustom[pdfRest Free Demo]pt file by including: <script src="/scripts/tlistCustomization.js"></script>
- Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
- Directly after the tlist tag, use a script s[pdfRest Free Demo]wing example for field(s) you want to change from input text[pdfRest Free Demo]The script will define the radio buttons that should be disp[pdfRest Free Demo]be assigned a variable name. Any variable name may be used. [pdfRest Free Demo]e repeated if more than one field needs to be displayed as a[pdfRest Free Demo]this case unique variable names must be used for each. After[pdfRest Free Demo]es the value options, the following command completes the script:
  
  ```
  tlistText2RadioButton('<FieldName>',<JavaScript_Variable_Name>);
  ```

**Note:** The following is the same cod[pdfRest Free Demo]rop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Dat e,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Re[pdfRest Free Demo] Status,Scholarship,Completion Date,Outcome,Notes;type:html]

<script>  
var rbValues = {};  
rbValues \['1']='Yes';  
rbValues \['2']='No';  
tlistText2RadioButton('Scholarship',rbValues); </script>

**Radio Button Example (Value/Name Pair)**

**Introduced in PowerSchool 8.0**

This version of the radi[pdfRest Free Demo]fers from the original version by allowing control over both[pdfRest Free Demo]displayed for the radio buttons and, separately, the value t[pdfRest Free Demo]e database.

- Within the <head> tag, add the tlistCusto[pdfRest Free Demo]ipt file by including: <script src="/scripts/tlistCustomization.js"></script>
- Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
- Directly after the tlist tag, use a script [pdfRest Free Demo]owing example for field(s) you want to change from input tex[pdfRest Free Demo] The script will define the radio buttons that should be dis[pdfRest Free Demo] be assigned a variable name. Any variable name may be used.[pdfRest Free Demo]be repeated if more than one field needs to be displayed as [pdfRest Free Demo] this case unique variable names must be used for each. Afte[pdfRest Free Demo]nes the options that should be displayed to users and a chos[pdfRest Free Demo]ed in the database, the following command completes the script:

```
tlistText2RadioButtonValNamePair('<FieldName>',<JavaScript_Variab le_Name>);
```

**Note:** The following is the same c[pdfRest Free Demo] drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Dat e,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,[pdfRest Free Demo]st Status,Scholarship,Completion Date,Outcome,Notes;type:html]

<script>  
var rbValues = \[];  
rbValues.push(\['Y','Yes']);  
rbValues.push(\['N','No']);  
tlistText2RadioButtonValNamePair('Scholarship',rbValues); </script>

**Text Area Example**

- Within the <head> tag, add the tlistCustomiz[pdfRest Free Demo] file by including: <script src="/scripts/tlistCustomization.js"></script>
- Within the <form> tag, use the tlist\_child or tlist\_standalone tag to add the auto-generated table.
- Directly after the tlist tag, use a script sim[pdfRest Free Demo]ng example for field(s) you want to change from input text t[pdfRest Free Demo]script will define the size of the text area that should be [pdfRest Free Demo]and be assigned a variable name. This script could be repeat[pdfRest Free Demo] field needs to be displayed as a text area.

```
tlistText2TextArea('<Field_Name>',<rows>,<columns>);
```

**Note:** The follo[pdfRest Free Demo]de as the original drop-down example, but only the tlist\_ch[pdfRest Free Demo]shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Dat e,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,[pdfRest Free Demo]st Status,Scholarship,Completion Date,Outcome,Notes;type:html]

<script>  
tlistText2TextArea('Notes',4,50);  
</script>

**Static/Read Only Text Example**

- Within the <head> tag, add the tlistCustomization.js JavaScript file by including: <script src="/scripts/tlistCustomization.js"></script>
- Within the <form> tag, use tlist\_child or tlist\_standalone tag to add the tlist auto-generated table.
- Directly after the tlist tag, use a script similar to th[pdfRest Free Demo] for field(s) you want to change from input text to read onl[pdfRest Free Demo] could be repeated if more than one field needs to be modified.

```
tlistText2StaticText('<Field_Name>');
```

**Note:** The f[pdfRest Free Demo]e code as the original drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Dat e,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Requ est Date,Request Status,Scholarshi[pdfRest Free Demo]utcome,Notes;type:html]

<script>  
tlistText2StaticText('Institution');  
</script>

**Static/Read Only After Submit Text Example**

**Introduced in PowerSchool 8.0**

This new op[pdfRest Free Demo] to be entered for a new field when it is originally blank, [pdfRest Free Demo] field read only.

- Within the <head> tag, add the tlis[pdfRest Free Demo]avaScript file by including: <script src="/scripts/tlistCustomization.js"></script>
- Within the <form> tag, use tlist\_child or tlist\_standalone tag to add the tlist auto-generated table.
- Directly after the tlist tag, use a[pdfRest Free Demo]the following example for field(s) you want to change from i[pdfRest Free Demo]nly text once they are saved. This script could be repeated [pdfRest Free Demo]eld needs to be modified.

```
tlistText2StaticTextAllowNew('<Field_Name>');
```

**Note:** The following is the same code as the original drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Dat e,Request\_St[pdfRest Free Demo]mpletion\_Date,Outcome,Notes;fieldNames:Institution,Requ est[pdfRest Free Demo]s,Scholarship,Completion Date,Outcome,Notes;type:html]

<script>  
tlistText2StaticTextAllowNew('Institution'); </script>

**Multiple Special Formatting Tags Example**

The foll[pdfRest Free Demo] all of the above examples used together.

**Note**: The follo[pdfRest Free Demo]de as the drop-down example, but only the tlist\_child and script are shown.

~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Dat e,Request\_Sta[pdfRest Free Demo]pletion\_Date,Outcome,Notes;fieldNames:Institution,Requ est [pdfRest Free Demo],Scholarship,Completion Date,Outcome,Notes;type:html]

<script>  
var InstValues = {};  
InstValues\['1']='PowerSchool University';  
InstValues\['2']='College of Standards';  
InstValues\['3']='University of DDA';  
tlistText2DropDown('INSTITUTION',InstValues); var rbValues = {};

rbValues \['1']='Yes';  
rbValues \['2']='No';  
tlistText2RadioButton('Scholarship',rbValues); tlistText2TextArea('Notes',4,50);  
tlistText2StaticText('Outcome');

</script>

**Using CSS Styles to Resize Input Fields**

As previously noted, the default width of all fields in a tlist\_child or tlist\_standalone table, except Bool[pdfRest Free Demo]e shown as a checkbox, is 20 characters wide (about 180px). [pdfRest Free Demo]of individual columns in the auto-generated HTML table use C[pdfRest Free Demo]ts (CSS).

Each column in the auto-generated HTML table will be part of a <colgroup> with each column in the table gi[pdfRest Free Demo]te equal to "col-" plus the field name. In our college application tlist\_child example, the Institution <col> tag [pdfRest Free Demo]="col-Institution". Set this value to approximately 20px hig[pdfRest Free Demo]tag.

Each <input> tag in the table will be tagged with [pdfRest Free Demo]qual to the field name. In our college application tlist\_ch[pdfRest Free Demo]stitution input tag would include class="Institution". Defin[pdfRest Free Demo]ese classes to control the width of the column.

The following[pdfRest Free Demo]definition of CSS styles for several columns in our tlist\_child table.

**Note**: Only the HTML code necessary to demonstr[pdfRest Free Demo]s been included.

<html>  
<head>  
:  
:  
<style>

.col-Institution {width:235px;}

.Institution {width:215px;}

.col-Request\_Date {width:110px;}

.Request\_Date {width:90px;}

.col-Request\_Status {width:80px;}

.Request\_Status {width:60px;}

.col-Scholarship {width:70px;}

.Scholarship {width:50px;}

</style>  
</head>  
<body>  
:  
:  
~\[tlist\_child:STUDENTS.U\_COLLEGEAPP.U\_APPLICATIONS;displaycols:Institution,Request\_Dat e,Request\_Status,Scholarship,Completion\_Date,Outcome,Notes;fieldNames:Institution,Requ est Date,Reque[pdfRest Free Demo]ip,Completion Date,Outcome,Notes;type:html]

**Using A Proper [pdfRest Free Demo](FRN) For Staff**

In order to allow Teachers to access multip[pdfRest Free Demo]single account with the Unified Teacher Record feature, it w[pdfRest Free Demo]it the TEACHERS table into two different tables – A USERS ta[pdfRest Free Demo]data directly related to the user, and the SCHOOLSTAFF table[pdfRest Free Demo]a directly related to the Teacher/School relationship. For m[pdfRest Free Demo]ation see knowledgebase article [Technical Information and F[pdfRest Free Demo]ed Teacher Record](https://help.powerschool.com/t5/PowerSchool-General/Technical-Information-and-Field-List-for-Unified-Teacher-Record/ta-p/12749) available on PowerSchool Community.

[pdfRest Free Demo]new table structure for staff will be important when creatin[pdfRest Free Demo]staff that use the tlist\_child tag. When creating a link to[pdfRest Free Demo]e the proper FRN must be part of the link. Historically this[pdfRest Free Demo]dding "?frn=~(frn)" to the end of the link. For example:

<[pdfRest Free Demo]rix.html?frn=~(frn)">

- **schedulematrix.html** is the pag[pdfRest Free Demo].
- **?frn=** is located immediately after the page address.
- **~(frn)** is a special tag that would insert the full prope[pdfRest Free Demo] member currently being viewed on the staff page. An FRN consists of two parts; the 3-digit table number and the DCID fie[pdfRest Free Demo]

In older versions of PowerSchool the ~(frn) tag would always[pdfRest Free Demo]ID. The Teachers table was table 005. So for a staff member [pdfRest Free Demo]the ~(frn) would return 005134. Starting with PowerSchool 7.[pdfRest Free Demo]o create database extensions to the Users table (204) or SchoolStaff table (203), it is critical to create page links tha[pdfRest Free Demo]RN. For example, you want to create a database extension to [pdfRest Free Demo] a custom web page to track teacher credentials. After creating a U\_Certificates extension table use the following tlist\_child tag on your custom web page to create, view, and delete records.

~\[tlist\_child:Users.U\_Credentials.U\_Certificates;displaycols:CredNum,CredType,CredIssu er,CredStart,CredEnd;fieldNames:Credential Number,Credential Type,Credential Issu[pdfRest Free Demo]es Date;type:html]

Because the records on this page all relat[pdfRest Free Demo]e (table 204), construct your page link using "**204~(\[teachers]USERS\_DCID)**" rather than ~(frn) or the tlist\_child t[pdfRest Free Demo]ion properly. This is what the link might look like:

<a hr[pdfRest Free Demo]l?frn=204~(\[teachers]USERS\_DCID)">Credentials</a>

[pdfRest Free Demo]e records in the tlist\_child table use the DCID field from [pdfRest Free Demo]her than the Teachers table.

**Direct Access to Database Extension Tables**

With PowerSchool version 8.0 or newer, a new HT[pdfRest Free Demo] to provide direct access to records in a database extension[pdfRest Free Demo]exible way than having to use tlist\_child or tlist\_standal[pdfRest Free Demo]e of this tag, custom pages can be created that provide acce[pdfRest Free Demo]or delete records in a one-to-many child table or a standalo[pdfRest Free Demo]itional web page design techniques.

To edit a record in the d[pdfRest Free Demo]l must know which table and record to edit. In most cases, t[pdfRest Free Demo]ing an FRN (file record number) with the page request. The F[pdfRest Free Demo]o parts. The first three digits represent the table, and the[pdfRest Free Demo] represent the record ID (often the DCID field in the table).

However, this won’t work for a database extension table beca[pdfRest Free Demo]fined table numbers.

So PowerSchool now has a new tag that al[pdfRest Free Demo]to these tables. The tag is called DirectTable.Select. This [pdfRest Free Demo] the table and the record and must be included between the f[pdfRest Free Demo]e. See Editing an Existing Record for exact details.

Use this[pdfRest Free Demo] existing record, add a new record, or delete an existing re[pdfRest Free Demo] table.

**DirectTable.Select**

The following is the format for[pdfRest Free Demo]lect HTML tag.

~\[ DirectTable.Select:<tableName>;<idColumn>:<idValue>]

The following provides additional[pdfRest Free Demo]s tag:

- The <tableName> is the name of the database ex[pdfRest Free Demo]the extension group.
- The <idColumn> is the name of th[pdfRest Free Demo] for the table. For database extension tables created throug[pdfRest Free Demo]nterface, this field name will always be “ID”.
- The <idValue> is the value of the primary key for the record to be edited.

The following is an example for a one-to-many table to[pdfRest Free Demo]ications. Note that only the actual one-to-many table name n[pdfRest Free Demo]ed, unlike the tlist\_child example earlier in this user gui[pdfRest Free Demo]values were needed: <CoreTableName>.<ExtensionGroup>.<ExtensionTable>.

This example shows editing a record in the U\_APPLICATIONS table with an ID = 41.

~\[DirectTable.Select:U\_APPLICATIONS;ID:41]

**DirectTable.Select and ~(gpv)**

Because it is impractical to enter an actual record ID value (i.e. 41) in the HTML code for the custom web page, the <idValue> can be replaced by PowerSchool’s GPV (Get Post Value) tag to dynamically enter the

ID number for the selected[pdfRest Free Demo] ID from one page to the next it is possible to include info[pdfRest Free Demo] URL that can be referenced by the target web page using the ~(gpv) tag. The proper format for this HTML tag is ~(gpv.passed\_label). Additional HTML tags are documented in knowledge base article [PowerSchool HTML Tags](https://help.powerschoo[pdfRest Free Demo]l-Customizations/PowerSchool-HTML-Tags/ta-p/14322) available[pdfRest Free Demo]munity.

The following is a simple example of adding post valu[pdfRest Free Demo]hat the first label and value comes after a question mark, w[pdfRest Free Demo] values are separated with an ampersand.

URL Request: /admin/home.html?my\_gpv\_1=50&my\_gpv\_2=65

GPV Tags: ~(gpv.my\_gpv\_1) and ~(gpv.my\_gpv\_2)

When using this technique with stude[pdfRest Free Demo]se extension tables that are children of the STUDENTS table,[pdfRest Free Demo]st pass both the FRN for the student record as well as the I[pdfRest Free Demo]e record. **Please see *Appendix B* for a complete set of ex[pdfRest Free Demo]source page with the list of existing records and the target[pdfRest Free Demo]ual records are edited.** The following example is taken fro[pdfRest Free Demo]e. The ~(id) information comes from the tlist\_sql results, [pdfRest Free Demo]d from the query has the proper record ID number ready to be[pdfRest Free Demo] page in the URL.

<a href="applications-edit.html?frn=~(studentfrn)&id=~(id)">

The following is an example of the Dir[pdfRest Free Demo]a GPV with a post value of “id”:

~\[DirectTable.Select:U\_APPLICATIONS;ID:~(gpv.id)]

**Where To Place the DirectTable.Select Tag**

As previously stated, the DirectTable.Select tag must [pdfRest Free Demo]e <form> tag. The tag should also be entered before your <input> tags for your fields because it impacts all [pdfRest Free Demo]me after it. Here is a simplified page layout example.

<html>  
<head></head>  
<body>  
<form>  
~\[DirectTable.Select:U\_APPLICATIONS;ID:~(gpv.id)] <table>  
<tr>  
<td>Field Label</td>  
<td><input type="text"...></td>  
</tr>  
</table>  
</form>  
</body>  
</html>

**Edit[pdfRest Free Demo]ord**

To edit one or more fields in an existing record in a d[pdfRest Free Demo]able four elements are required.

- The page URL should contai[pdfRest Free Demo] the required <idValue> applications.html?id=24 (if it[pdfRest Free Demo]me other way)
- The DirectTable tag after the <form> ta[pdfRest Free Demo]elds. ~\[DirectTable.Select:U\_APPLICATIONS;ID:~(gpv.id)]
- I[pdfRest Free Demo]age form. The proper way to reference a field when using DirectTable.Select is:  
  <input name="**\[ExtensionTable]Field\_Name**" value="">
- The standard hidden input tag must [pdfRest Free Demo]form fields, usually near the Submit button. For the admin portal that tag is:  
  <input type="hidden" name="ac" value="prim">

**Adding a New Record**

Adding a new record to an E[pdfRest Free Demo]one by using an <idValue> of -1.

~\[ DirectTable.Select:<tableName>;<idColumn>:-1]

When the <idValue> -1 in the DirectTable tag is used, all field values entere[pdfRest Free Demo]sed to create the new record and populate it with the entered data. **Please see *Appendix B* for a complete set of examp[pdfRest Free Demo] record page.**

**Adding a New Record to a One-to-one Extensi[pdfRest Free Demo]arent Record**

When the desired record ID in the parent table[pdfRest Free Demo]able is known, that value can be passed in a hidden <input> tag.

The format of that tag should be:

<input type="hi[pdfRest Free Demo]_Table.Parent\_Key\_Field" value="recordID">

The following[pdfRest Free Demo]dding a record to the U\_LAPTOP one-to-one extension of the [pdfRest Free Demo]e the desired STUDENTS.DCID is 1091:

<td>  
~\[DirectTable.Select:U\_LAPTOP;STUDENTSDCID:-1] Barcode:<input type="text" name="\[U\_LAPTOP]Barcode" value=""> <input type=[pdfRest Free Demo]ENTS.DCID" value="1091"> </td>

**Adding a New Record to a One-to-One Extension and Fields in the Parent Table**

In[pdfRest Free Demo]ple DirectTable will be used twice. A new record in the Dist[pdfRest Free Demo]will be added first and the new record's primary key value w[pdfRest Free Demo]ts extension table DistrictCalendar\_Ext.

<td> <!-- The parent table -->

~\[DirectTable.Select:DistrictCalendar;ID:-1]

CalendarName: <input type="text" name="\[DistrictCalendar]CalendarName" value=""> StartDate:

EndDate: </td>

<input type="text" name="\[DistrictCalendar]StartDate" value=""> <input type="text" name="\[DistrictCalendar]EndDate" value="">

<td> <!-- The child table -->  
~\[DirectTable.Select:DistrictCalendar\_Ext;DistrictCalendarID:-1]

Grade\_Level:<input type="text" name="\[DistrictCalendar\_Ext]Grade\_Level" value=""> </td>

**Adding a New Record to a One-to-Many Extension of a Child Table**

It is [pdfRest Free Demo]tiple DirectTable.Select tags on a single page to add a reco[pdfRest Free Demo]ted tables all at once. Using the college application exampl[pdfRest Free Demo]asked for the ability to track multiple notes per college ap[pdfRest Free Demo]f just one. This one-to-many child table will be used instea[pdfRest Free Demo]otes field.

U\_APPLICATIONS (which is a child of STUDENTS) now has its own one-to-many child table (i.e. U\_APP\_NOTES) to[pdfRest Free Demo]note contents, date, and author. For this example, a new rec[pdfRest Free Demo] with fields from the U\_APPLICATIONS table, which will be i[pdfRest Free Demo]the new record's primary key value will be applied to its child table U\_APP\_NOTES.

~\[DirectTable.Select:U\_APPLICATIONS;ID:-1]

<div>  
<label style="width: 140px">Institution:</label> <input type="text" name="\[U\_APPLICATIONS]Institution" value=""> </div>  
:  
: (Other U\_A[pdfRest Free Demo]here)  
:  
~\[DirectTable.Select:U\_APP\_NOTES;ID:-1]  
<div>  
<label style="width: 140px">Notes:</label>  
<textarea name="\[U\_APP\_NOTES]Note" cols="50" rows="5"> </div>  
<div>  
<label style="width: 140px">Note Author:</label> <input type="text" name="\[U\_APP\_NOTES]Author" value=""> </div>  
<div>  
<label style="width: 140px">Note Date:</label> <input type="text" name="\[U\_APP\_NOTES]Note\_Date" value=""> </div>

Note: In this example only the initial fi[pdfRest Free Demo] added to the U\_APP\_NOTES table. Other techniques would be[pdfRest Free Demo]tional records, such as using DirectTable.Select on its own web page.

**Adding a New Record to a One-to-one Extension of a Child Table**

Adding a new record to a one-to-one extension o[pdfRest Free Demo]similar to adding a record to a one-to-many child of a child[pdfRest Free Demo]using the college application example, U\_APPLICATIONS has its own one-to-one extension table (i.e. U\_APP\_OPTIONS) to t[pdfRest Free Demo]/no flags about each application. For this example, a new re[pdfRest Free Demo]d with fields from the U\_APPLICATIONS child table, which wi[pdfRest Free Demo]t, and the new record's primary key value will be applied to[pdfRest Free Demo]e U\_APP\_OPTIONS.

~\[DirectTable.Select:U\_APPLICATIONS;ID:-1]

<div>  
<label style="width: 140px">Institution:</label> <input type="text" name="\[U\_APPLICATIONS][pdfRest Free Demo]""> </div>  
:  
: (Other U\_APPLICATIONS fields here)  
:  
~\[DirectTable.Select: U\_APP\_OPTIONS;ID:-1]  
<div>  
<label style="width: 140px">Student Has Signed:</label> <input type="checkbox" name="\[U\_APP\_OPTIONS]Student\_Signature" value="1"> </div>  
<div>  
<label style="width: 140px"> Guardians Have Signed:</label> <input type="checkbox" name="\[U\_APP\_OPTIONS]Parent\_Signature" value="1"> </div>  
<div>  
<label style="width: 140px">All Attachments Received:</label> <input type="checkbox" name="\[U\_APP\_OPTIONS]Attachments" value="1"> </div>

Note: In this exampl[pdfRest Free Demo]first record would be added to the U\_APP\_OPTIONS table. Ot[pdfRest Free Demo]d be needed to add additional records, such as using DirectT[pdfRest Free Demo]own web page.

**Deleting a Record**

To delete an existing reco[pdfRest Free Demo]tension table three elements are required.

- The page URL sho[pdfRest Free Demo]value with the required <idValue> applications-edit.html?id=24
- A special hidden input tag for the page form. The proper format is:
  
  ```
  <input type="hidden" name="
  DD-<tableName>.<idColumn>:<idValue>
  "
  value="1">
  ```
- The standar[pdfRest Free Demo]must be included in the form fields, usually near the Submit[pdfRest Free Demo]s:  
  <input type="hidden" name="ac" value="prim">

For [pdfRest Free Demo]tion example, the hidden <input> tag would be formatted as:

input type="hidden" name="DD-U\_APPLICATIONS.ID:~(gpv.id)" value="1">

**Note:** For many uses the Delete button wil[pdfRest Free Demo] own separate <form> tag from the main page to keep th[pdfRest Free Demo]arate from the Submit button. Please review the example for *Edit a Record* in Appendix B.

**Custom Insertion Points and Page Fragments**

Insertion points are special locations within [pdfRest Free Demo]a page where customizers can more easily insert dynamic content (page fragments).

PowerSchool insertion points have the fo[pdfRest Free Demo]tics:

- With insertion points, the original source page does [pdfRest Free Demo]omized in order to add new content to that page. This can he[pdfRest Free Demo] down on the number of custom pages that need to be created [pdfRest Free Demo]dated when a new version of PowerSchool is released.
- Page f[pdfRest Free Demo]amically inserted in to the default source page.
- Multiple s[pdfRest Free Demo]-provided insertion points can exist on a page, and new inse[pdfRest Free Demo] added.
- You can physically move fragments around on the page using client-side DOM manipulation via standardized XML met[pdfRest Free Demo]sing JavaScript.
- You can customize every existing and new p[pdfRest Free Demo]

**How it Works**

PowerSchool has specified a set of standard [pdfRest Free Demo]his set of insertion points is expandable, but primarily con[pdfRest Free Demo]mber of predefined places on the page. The standardized inse[pdfRest Free Demo]nerally placed in common header and footer wildcards and sig[pdfRest Free Demo] menus. This allows standard insertion points to be defined [pdfRest Free Demo]ty of pages in PowerSchool with minimal effort.

You decide which page(s) to customize and then choose an insertion point o[pdfRest Free Demo]ese two pieces of information, a page fragment file can be c[pdfRest Free Demo] file system (often referred to as the custom web\_root) or [pdfRest Free Demo]age Management feature in the PowerSchool System Administrat[pdfRest Free Demo]n rendering the page, PowerSchool will gather together all t[pdfRest Free Demo]sertions for that page and render them as inline HTML with t[pdfRest Free Demo]each insertion point may have multiple inserted page fragmen[pdfRest Free Demo]ge and will all be rendered on the page.

See *Appendix A* for[pdfRest Free Demo] insertion points.

**Page Fragments**

A page fragment is simpl[pdfRest Free Demo]ent to be added to a target page. It could be something simp[pdfRest Free Demo]ng example:

<p>Hello world!

I'm an auto-inserted page fragment.</p>

Or, a page fragment could be a complex combi[pdfRest Free Demo] and jQuery scripts. Because page fragments will be inserted[pdfRest Free Demo]erSchool HTML pages they do not require any of the standards HTML <head>, <body>, or other tags. The main pag[pdfRest Free Demo]those tags.

**Standard Insertion Points**

PowerSchool includes[pdfRest Free Demo]insertion points available on every page. This means that, t[pdfRest Free Demo] need to think about insertion points: the common ones will [pdfRest Free Demo]. This also has a benefit in allowing standardized naming: y[pdfRest Free Demo]e page where the footer insertion point is called **content.footer** and another where it is called **content\_footer**.

T[pdfRest Free Demo]ion points should be available on every page in PowerSchool:

- **content.header** top of the page above the blue bar
- **content.footer** near the bottom of the page, above the copyrig[pdfRest Free Demo]ontent area
- **leftnav.footer** right below the left navigat[pdfRest Free Demo]owerSource and/or Mobile App content
- **page.header** – loca[pdfRest Free Demo]onscripts wildcard (should rarely be used)

The following exam[pdfRest Free Demo]rst few lines of code from the admin\_footer\_css wildcard:

<div id="cust-content-footer">~\[cust.insertion\_point:content.footer]</div> <div id="legend" style="display:none;">  
<h3>~\[text:psx.txt.wildcards.admin\_footer\_css.legend]</h3> </div>  
</div><!--end content-main-->

The very first line defines an insertion [pdfRest Free Demo]f content.footer.

**Special Cases**

On the Visual Scheduler an[pdfRest Free Demo]es, the content.footer will be hidden and unsupported.

**Spec[pdfRest Free Demo]s**

You can add your own insertion points. Note that the stan[pdfRest Free Demo]ts use this same naming scheme, just in standard header and [pdfRest Free Demo]fine an insertion point within the HTML of a PowerSchool pag[pdfRest Free Demo]g tag:

~\[cust.insertion\_point:POINTNAME;DEFAULT\_CONTENT]

Th[pdfRest Free Demo]ired and is the name of the insertion point: it should be in dot-separated form, such as "page.header", "leftnav.footer",[pdfRest Free Demo]". When adding your own insertion points to a page, be sure [pdfRest Free Demo]ames.

The DEFAULT\_CONTENT is an optional block of content th[pdfRest Free Demo] in the page if no insertions are found for this insertion p[pdfRest Free Demo]e used rarely. If there is no associated content with an ins[pdfRest Free Demo]age, the point should be invisible.

The following example add[pdfRest Free Demo]oint named "help.pages":

~\[cust.insertion\_point:help.pages]

[pdfRest Free Demo]ion point in an FTL file, you can use a similar construction[pdfRest Free Demo]r syntax:

<@cust.insertion name="POINTNAME">DEFAULT\_CONTENT</@cust.insertion>

The following example adds a new[pdfRest Free Demo]med "help.pages" to an FTL file in the /admin/ftl/ directory:

<@cust.insertion name="help.pages"></@cust.insertion>

**Auto-Insertions: How to Use Defined Insertion Points**

An auto-insertion is simply the act of taking one or more def[pdfRest Free Demo] and dynamically loading them in to the designated PowerScho[pdfRest Free Demo]ified insertion points. There are two ways to define auto-insertions:

- **URL-based**. The page fragment chosen is based on the URL (Uniform Resource Locator) used by the browser to r[pdfRest Free Demo]m the system.
- **Wildcard-based**. The page fragment chosen [pdfRest Free Demo]ds that are included on the page using the HTML ~\[wc:WILDCARD\_FILE] syntax.

These operate essentially identically. The o[pdfRest Free Demo]n how the system constructs the conventional file name to fe[pdfRest Free Demo]om the file system or Custom Web Page Management application[pdfRest Free Demo]lidation and language translation functionality can be appli[pdfRest Free Demo]s.

**URL-Based Auto-Insertion of Page Fragments**

When creatin[pdfRest Free Demo]the name of that file is critical for proper operation. In URL-based auto-insertions, the source page URL is used in cons[pdfRest Free Demo]f the page fragment. For example, the URL of the page to be [pdfRest Free Demo]ollowing:

http://<server\_address>/admin/some\_directory/some\_page.html

Note: The file's extension may be any of the[pdfRest Free Demo]erSchool URL extensions, including **.**html, .htm, and .action.

Upon processing a page with this URL, the customization m[pdfRest Free Demo] the insertion points on the page, looking for a page fragme[pdfRest Free Demo]ng name:

/admin/some\_directory/some\_page.FRAGMENT\_NAME.INSERTION\_POINT\_NAME.txt

The page fragment file name is constru[pdfRest Free Demo]arts:

- **/admin/some\_directory** – a page fragment file mus[pdfRest Free Demo]same directory as the source page's file
- **some\_page** pre[pdfRest Free Demo]e as the name of the source page, without the extension (i.e. html).
- **FRAGMENT\_NAME** any arbitrary name to help ident[pdfRest Free Demo]nt and keep its name unique. PowerSchool allows multiple fra[pdfRest Free Demo]ed in to the same page without impacting each other. If mult[pdfRest Free Demo]insertions are defined for a page, the insertion order is in[pdfRest Free Demo]ed.
- **INSERTION\_POINT\_NAME** – must match the name of the[pdfRest Free Demo] be used in the page to be customized (i.e. "content.footer").

<!--THE END-->

- **.txt** – page fragments are always named [pdfRest Free Demo]t".

**Example**

District administrators have requested that a [pdfRest Free Demo]y phone numbers be placed just below the What's New box on t[pdfRest Free Demo]ining the HTML source code for /admin/home.html shows you:

73

<!-- end of search menu -->

- 74 </form>
- 75 </div>
- 76 ~\[wc:admin\_startpage\_whats\_new]
- 77 ~\[wc:admin\_footer\_css]

Line 76 inserts the wildcard /wildcards/admin\_startpage\_whats\_new.txt

Line 77 inserts the wildcard /wildcards/admin\_footer\_css.txt, which begins with a content.foo[pdfRest Free Demo]. If you create a page fragment and use the content.footer i[pdfRest Free Demo]he Start Page, the table of emergency numbers would be displ[pdfRest Free Demo] What's New box as requested.

This is the proper file name fo[pdfRest Free Demo]e page fragment, assuming a fragment name of "Emergency\_Numbers" is used:

The page fragment file would be placed in the /[pdfRest Free Demo]e that is the same location as our targeted file, /admin/home.html.

Example of /admin/home.Emergency\_Numbers.content.foot[pdfRest Free Demo]t:

<div class="box-round">  
<h2 class="toggle expanded">Emergency Numbers</h2> <ul class="text">

<li>Police/Fire/Ambulance: 911</li>

<li>Poison Control: 1-800-222-1222</li>

<li>Superintendent's Office: 555-555-1000</li>

<li>General Hospital: 555-555-0911</li>

<li>Children's Hospital: 555-555-2300</li>

<li>Gas Leak: 888-555-6000</li>

<li>Mechanical Issues: 555-555-1043</li>

<li>Di[pdfRest Free Demo]tification System: 877-555-9911</li> </ul>  
</div>

Example screenshot of results:

**Wildcard-Based Auto-I[pdfRest Free Demo]agments**

Wildcard-based auto-insertions are exactly the same as URL-based insertions, except for the method of determining the file name:

/wildcards/some\_wildcard\_name.EXTENSION\_NAME.INSERTION\_POINT\_NAME.txt

Note that the content will be in[pdfRest Free Demo]e where this wildcard is used. This means you can cause the [pdfRest Free Demo]nt to show up on many pages by associating with a common wil[pdfRest Free Demo]used on all of those pages. For example, since commonscripts[pdfRest Free Demo] every PowerSchool HTML page, you can cause content to be in[pdfRest Free Demo]f every page in the system by creating a file named like:

/wi[pdfRest Free Demo]ts.EXTENSION\_NAME.page.header.txt

Note that the page fragmen[pdfRest Free Demo]be placed in the /wildcards folder.

**Moving Inserted Page Fr[pdfRest Free Demo]Location On the Target Page**

In many cases the predefined in[pdfRest Free Demo]t the location on the page where you would like to dynamical[pdfRest Free Demo] fragment contents. Rather than adding a custom insertion po[pdfRest Free Demo]ich would once again require modifying the source page, diff[pdfRest Free Demo]n be used to dynamically move the contents of the page fragm[pdfRest Free Demo]location on the page. Examples might include adding a link t[pdfRest Free Demo] menu (/admin/students/more2.html), adding a link to a custo[pdfRest Free Demo]tem Reports menu, or adding additional input fields to the s[pdfRest Free Demo] page. In each of these examples our page fragments could be[pdfRest Free Demo] using the standard insertion points on each of those pages,[pdfRest Free Demo]ent content would look out of place near the bottom of each [pdfRest Free Demo] content.footer insertion point. Use one of the following me[pdfRest Free Demo]content.

**XML-Based Movement of Page Fragments**

To move your[pdfRest Free Demo]ents to a specified location on the target page, create an X[pdfRest Free Demo]red with your page fragment file. As with page fragments, th[pdfRest Free Demo]ile is critical. Create an XML file with the following naming convention:

/admin/some\_directory/some\_page.FRAGMENT\_NAME.INSERTION\_POINT\_NAME.xml

The name is the same as the page f[pdfRest Free Demo]paired with, except with an .xml file extension instead of a[pdfRest Free Demo]n.

To move your content to a different location on the page, [pdfRest Free Demo] XML file should use the following format:

- 1 <insertionMetadata xmlns="[http://www.powerschool.com](http://www.powerschool.com)"
  
  2
  
  3
  
  4
  
  xmlns:xsi="[http://www.w3.org/2001/XMLSchema-instance](http://www.w3.org/2001/XMLSchema-instance)" xsi:schemaLocation="[http://www.powerschool.com](http:[pdfRest Free Demo]om) insertionmetadata.xsd"> <inject location="h1" how="before" />
- 5 </insertionMetadata>

Only the contents[pdfRest Free Demo] change. The attributes of the inject tag on line four have [pdfRest Free Demo]ngs:

- **location** The location in the page where the conten[pdfRest Free Demo] page fragment is to be inserted. The location is a jQuery s[pdfRest Free Demo]h can have any CSS3 selector value plus extensions supported by jQuery.
- **how** How the injection is to be done. The fol[pdfRest Free Demo] (which operate in the same manner as their jQuery counterparts) are supported:

|   |            |                                                                                                                                                                                                               |
|---|------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| » | **before** | Inject the content as a block before the given location                                                                                                                                                       |
| » | **after**  | Inject the conte[pdfRest Free Demo] the given location                                                                                                                                                        |
| » | **insert** | Insert the content as a block at the beginning of th[pdfRest Free Demo]nt at the given location. For example, an "insert" for the "[pdfRest Free Demo]t the content at the beginning of an <h1> element |
| » | **append** | Insert the content as a block at the end of t[pdfRest Free Demo]iven location                                                                                                                                 |

**Example**

In the page fragment example[pdfRest Free Demo]rators requested that a table with emergency phone numbers b[pdfRest Free Demo] the What's New box on the Start Page. It has been decided t[pdfRest Free Demo]ne numbers would look better in a dialog popup window trigge[pdfRest Free Demo]an emergency icon. The location of this icon should be to th[pdfRest Free Demo]s "Start Page". After updating the contents of our page frag[pdfRest Free Demo] new icon and link to our popup window we are ready to creat[pdfRest Free Demo]lace the icon in the desired location instead of the default[pdfRest Free Demo]ertion point below the What's New box.

Our file name would be:

/admin/home.Emergency\_Numbers.content.footer.xml

The content[pdfRest Free Demo]ould look like this example:

<insertionMetadata xmlns="[http://www.powerschool.com](http://www.powerschool.com)" xmlns:xsi="[http://www.w3.org/2001/XMLSchema-instance](http://www.w[pdfRest Free Demo]a-instance)" xsi:schemaLocation="[http://www.powerschool.com](http://www.powerschool.com) insertionmetadata.xsd"> <[pdfRest Free Demo]" how="append" />  
</insertionMetadata>

Note the **location** attribute is "h1" because "Start Page" is wrapped in an <h1> tag. The **how** attribute is set to "append"[pdfRest Free Demo]r content after "Start Page" but before the closing <h1> tag.

Example screenshot of results:

**jQuery-Based Movement of Page Fragments**

A second method to move your page fragment[pdfRest Free Demo]fied location on the target page is through the use of jQuer[pdfRest Free Demo] included with PowerSchool. When using jQuery it is not nece[pdfRest Free Demo]eparate file as described in "XML-Based Movement of Page Fra[pdfRest Free Demo]page fragment content is wrapped within a jQuery script.

The [pdfRest Free Demo]isplays sample code using jQuery rather than an XML file for[pdfRest Free Demo]ers example:

<script>  
$j("h1").append('\\  
<span style="align:right;position:relative;z-index:10;">\\ <a c[pdfRest Free Demo]itle="Emergency Numbers" href="#hiddenDivDialog">\\ <i[pdfRest Free Demo]rgency\_number.png">\\  
</a>\\  
</span>')

</script>

<div id="hiddenDivDialog" class="hide">  
<ul class="text">  
<li>Police/Fire/Ambulance: 911</li>  
<li>Poison Control: 1-800-222-1222</li>  
<li>Superintendent's Office: 555-555-1000</li> <li>General Hospital: 555-555-0911</li>  
<li>Children's Hospital: 555-555-2300</li>  
<li>Gas Leak: 888-555-6000</li>  
<li>Mechanical Issues: 555-555-1043</li>  
<li>District Emergency Notification System: 877-555-9911</li> </ul>

</div>

[pdfRest Free Demo]of results after clicking on the telephone icon:

**Upload Custom Web Page Files**

To upload Custom Web Page files, use the [pdfRest Free Demo]Administrator application.

**Plugin Packages**

Plugin packages[pdfRest Free Demo] distribute custom solutions between different PowerSchool s[pdfRest Free Demo]n of a plugin package builds a single complete zip file cont[pdfRest Free Demo]ensions, custom pages and page fragments, and any associated[pdfRest Free Demo]have been extracted from the custom pages included in the pa[pdfRest Free Demo] include one or more language translations of text on a custom page.

**ZIP File Format**

The zip file must be in a specific[pdfRest Free Demo]ts of the zip file that do not conform to the following spec[pdfRest Free Demo]ignored:

- An XML file named "plugin.xml" must be in the root[pdfRest Free Demo]ip file. This file is mandatory. If the uploaded zip file do[pdfRest Free Demo]the plugin installation process will fail.
- Zero or more dat[pdfRest Free Demo]inition files under the zip file directory "user\_schema\_ro[pdfRest Free Demo] file describing a database extension.
- Zero or more page cu[pdfRest Free Demo]nder the zip file directory "WEB\_ROOT". These may include v[pdfRest Free Demo] files listed below under "Installing a Plugin Package".
- Ze[pdfRest Free Demo]ey properties files directly under the zip file directory "M[pdfRest Free Demo]must be one file per localization. The name of the MessageKe[pdfRest Free Demo]s PluginName.*locale*.properties where locale is the upper c[pdfRest Free Demo]y code, followed by an underscore, followed by the lower cas[pdfRest Free Demo] code. For example: PluginName.US\_en.properties.
- Zero or m[pdfRest Free Demo]erties files directly under the zip file directory "queries\_root". The PowerQuery file may contain multiple named querie[pdfRest Free Demo]epresent a group of or a set of related queries. Multiple ph[pdfRest Free Demo]iles may also be created. All the files must be included in [pdfRest Free Demo]he name of the PowerQuery properties file is *uniquename*.named\_queries.xml. For example: agsd\_search\_laptops.named\_queries.xml.
- Zero or more Permission Mapping files directly u[pdfRest Free Demo]irectory "permissions\_root". If you are building custom pag[pdfRest Free Demo]use PowerQueries in those pages, or if you want to use other[pdfRest Free Demo]those pages, you must declare a permission mapping file and [pdfRest Free Demo] your plugin package. You may declare as many permission map[pdfRest Free Demo]ed, but each permission mapping file can contain many entrie[pdfRest Free Demo]Permission Mapping properties file is *uniquename*.permission\_mappings.xml. For example: agsd\_search\_laptops.permission\_mappings.xml.

**MessageKey Properties File Format**

MessageK[pdfRest Free Demo] are auto-generated when the Plugin Package is created and n[pdfRest Free Demo] manually. They contain a list of custom message keys used o[pdfRest Free Demo]om page. Each line in the file contains a Key Name = Value p[pdfRest Free Demo]e is:

Upload Custom Web Page Files

psx.htmlc.directory\_path.pagename.original\_text={translated text}

- psx.htmlc – the sta[pdfRest Free Demo]ssagekeys on customizations
- directory\_path – an underscore[pdfRest Free Demo]of the path
- riginal\_text – the original text with spaces r[pdfRest Free Demo]res

For example, CA\_fr (Canadian French) key=value pairs for[pdfRest Free Demo]ting System" and "Model Number" on page /admin/students/lapt[pdfRest Free Demo]like this:

psx.htmlc.admin\_students.laptop.operating\_system=Système d'exploitation psx.htmlc.admin\_students.laptop.model\_number= Numéro de modèle

**Plugin Package Example Layout**

T[pdfRest Free Demo]example layout for a "Laptop.zip" plugin package. The file n[pdfRest Free Demo] as shown. The user\_schema\_root folder will not be include[pdfRest Free Demo] contains only web page files. The MessagerKeys folder will [pdfRest Free Demo]a package that does not contain custom message keys (foreign[pdfRest Free Demo]ons). The permissions\_root and queries\_root folders are op[pdfRest Free Demo] no query or permission mapping files.

It is possible to unzi[pdfRest Free Demo]you download from the Internet, examine or change the conten[pdfRest Free Demo]erSchool installation, and then re-zip the file to be import[pdfRest Free Demo] you wanted to change the field names used in the package yo[pdfRest Free Demo]database extension XML file and any web pages that referenced those fields.

**Creating a Plugin Package**

To create a plugi[pdfRest Free Demo]to the Create Plugin Package page, select which assets shoul[pdfRest Free Demo] click the **Create Plugin Zip File** button.

1. Sign in at t[pdfRest Free Demo]
2. Navigate to https://<server>/admin/customization/Cr[pdfRest Free Demo]ion The Create Plugin Package page appears.
3. Use the follow[pdfRest Free Demo]information in the fields: Note: Asterisks indicate required fields.

|                             |                                                                                                                                                                                                                                                                                                                                           |
|-----------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Field**                   | **Description**                                                                                                                                                                                                                                                                                                                           |
|[pdfRest Free Demo]          | Enter a name for the plugin file.                                                                                                                                                                                                                                                                                                         |
| Plugin Version              | Enter the version of this plugin file.                                                                                                                                                                                                                                                                                                    |
| Plugin Description          | Enter a description of the plugin.                                                                                                                                                                                                                                                                                                        |
| Publisher Name              | Enter the name of the person who is creating this plugin.                                                                                                                                                                                                                                                                                 |
| Publisher Contact Email     |[pdfRest Free Demo]email for the person creating this plugin.                                                                                                                                                                                                                                                                              |
| Plugin File Name            | Enter the file name [pdfRest Free Demo] .zip extension will automatically be added.                                                                                                                                                                                                                                                       |
| Select Individual Files     | Navigate to and select the files to be in[pdfRest Free Demo]ge. Only modified or added files appear. The list includes b[pdfRest Free Demo]atabase extensions, which are listed under the "user\_schema\_root" folder at the end of the list. When a file is selecte[pdfRest Free Demo]e Selected Files box.                              |
| Select[pdfRest Free Demo]sk | Select files by searching for the file name. Enter the [pdfRest Free Demo]earch for and click Search. All files matching the term are [pdfRest Free Demo]ed Files box. Use an asterisk as a wildcard character. For example: lap* would add the file /admin/students/laptop.html or \*.html would add all custom files that end in .html. |
| Se[pdfRest Free Demo]       | A list of all selected files appears. Select one or[pdfRest Free Demo]ck **Remove Selected** to clear those selections from the li[pdfRest Free Demo]                                                                                                                                                                                     |

4\.

Click **Create Plugin Zip File**. The system creates the r[pdfRest Free Demo]file and packages it and the selected extension files in one[pdfRest Free Demo] can download.

**Note**: If your browser preference is set to[pdfRest Free Demo]ter downloading, disable this preference before creating the[pdfRest Free Demo]se, you will have to re-zip the package files before you imp[pdfRest Free Demo]the Plugin Install page.

**Note**: When a plugin containing a one-to-one database extension of the STUDENTS table is insta[pdfRest Free Demo]he ReportWorks service needs to be restarted before the new [pdfRest Free Demo]sed within ReportWorks. To restart the service, open the Pow[pdfRest Free Demo] On the Start page, click **Restart ReportWorks Services**. [pdfRest Free Demo]ne from within PowerSchool by navigating to System Setup > System Settings > Reset Server. Check only the box for "[pdfRest Free Demo]mcat service (includes ReportWorks)".

**Installing a Plugin Package**

Plugin Management is used to import, house, and manag[pdfRest Free Demo] extensions and web customizations. You can import zipped pl[pdfRest Free Demo]contain a plugin definition file and at least one web file o[pdfRest Free Demo]n.

The plugin package can include one or more of the followin[pdfRest Free Demo]e .zip file:

- » Database extension definition .xml files
- » Custom pages
- » Page fragments
- » Page fragment XML files
- » Image files (GIF, PNG, JPEG, JPG)
- » PDF files (PDF)
- » CSS files
- » JavaScript files
- » Other web directory artifacts
- » [pdfRest Free Demo]es files
- » PowerQuery properties files
- » Permission Mappin[pdfRest Free Demo]

**How to Import a Plugin Package**

Use the following procedur[pdfRest Free Demo]tall a plugin package.

1. Sign in to PowerSchool.
2. Navigate to System > System Settings > Plugin Management Configu[pdfRest Free Demo]Management Dashboard page appears.
3. Click Install.
4. Click [pdfRest Free Demo]or the " Plugin Installation File" and locate the .zip file [pdfRest Free Demo]lugin package, then click Install.

**Note:** If any of the pl[pdfRest Free Demo]you are importing already exist in the Custom Web Page Manag[pdfRest Free Demo]e, PowerSchool will display an error message with details of[pdfRest Free Demo]e plugin file will not be installed. This is to help ensure [pdfRest Free Demo]ins do not overwrite each other.

**Important Information on P[pdfRest Free Demo]e/Disable/Delete**

When the **Disable** function is selected [pdfRest Free Demo] Plugin Management Dashboard, all associated assets (databas[pdfRest Free Demo]customizations, message keys, etc.) are disabled as well. Cu[pdfRest Free Demo]s associated with a plugin package will not be served from t[pdfRest Free Demo]Manager while a plugin is disabled. Currently, PowerSchool d[pdfRest Free Demo]anism to indicate if a custom page is associated with a plug[pdfRest Free Demo]he Custom Web Page Manager.

When the **Delete** function is s[pdfRest Free Demo]n on the Plugin Management Dashboard, all file assets associ[pdfRest Free Demo]are deleted. However, deleting the plugin will not delete an[pdfRest Free Demo] from the Oracle database that were created by a database ex[pdfRest Free Demo]

**Appendix A: List of Insertion Points**

Rows with bold text [pdfRest Free Demo]rtion point since the previous version of this document.

|                                                             |                               |
|-------------------------------------------------------------|-------------------------------|
| **Page URL**                                                | **Insertion Point(s)**        |
| /admin/constraints/men[pdfRest Free Demo]                   | leftnav.footer                |
| /admin[pdfRest Free Demo]                                   | leftnav.footer                |
| /admin/powerschedule/menu.html                              | leftnav.footer                |
| /admin/powerschedule/menu\_task\_nav.html                   | leftnav.footer                |
| /admin/powerschedule/constraints/menu.html                  | leftnav.footer                |
| /admin/pow[pdfRest Free Demo]talog/menu.html                | leftnav.footer                |
| /admin/powerschedule/coursegroups/menu.html                 | leftnav.footer                |
| /admin/powerschedule/[pdfRest Free Demo]                    | leftnav.footer                |
| /admi[pdfRest Free Demo]uestsetup/menu.html                 | leftnav.footer                |
| /admin/powerschedule/sections/menu.html                     | leftnav.footer                |
| /admin/powersche[pdfRest Free Demo]html                     | leftnav.footer                |
| [pdfRest Free Demo]rttabs.html                              | report.tabs                   |
| /admin/sections/menu.html                                   | leftnav.footer                |
| /admin/stud[pdfRest Free Demo]assprintresults.html          | leftnav.footer                |
| /admin/studentlist/counselor/menu.html                      | leftnav.footer                |
| /admin/students/more2.[pdfRest Free Demo]                   | leftnav.footer                |
| /admin[pdfRest Free Demo]m.html                             | leftnav.footer                |
| /admin/teacherschedules/menu.html                           | leftnav.footer                |
| /teachers/menu.ht[pdfRest Free Demo]                        | leftnav.footer                |
| /wildcards/admin\_footer\_css.txt                           | [pdfRest Free Demo]           |
| /wildcards/admin\_footer\_frame\_css.txt                    | content.footer                |
| /wildcards/admin\_header\_css.txt                           | content.hea[pdfRest Free Demo]|
| /wildcards/admin\_header\_frame\_css.txt                    | content.header                |
| /wildcards/admin\_header\_frame\_sched\_css.txt             | content.header                |
| /wildcards/admin\_nav\_menu\_left\_css.txt                  | leftnav.footer                |
| **Page URL**                                                | **Insertion Point(s)**        |
| /wildcards/commonscripts.txt                                | page.header                   |
| /wildcards/guardian\_footer.txt                             | content.[pdfRest Free Demo]   |
| /wildcards/guardian\_footer\_yui.txt                        | content.footer                |
| **/wildcards/guardian\_footer\_yui\_limited.txt**           | **content.footer**            |
| /wildcards/guardian\_header.txt                             | content.header leftnav.footer |
| /wildcards/guardian\_header\_yui.txt                        | content.header leftnav.footer |
| **/wildcards/guardian\_header\_yui\_limited.txt**           | **content.header**            |
| /wildcards/sm\_psadmin\_\_no\_admin\_nav\_menu\_content.txt | conte[pdfRest Free Demo]      |
| /wildcards/sm\_psadmin\_content.txt                         | content.footer leftnav.footer |
| /wildcards/sm\_psguardian\_content.txt                      | content.footer leftnav.footer |
| /wildcards/sm\_psteacher\_content.txt                       | content.footer leftnav.footer |
| /wildcards/subs\_footer\_css.txt                            | content.f[pdfRest Free Demo]  |
| /wildcards/subs\_header\_css.txt                            | content.header                |
| /wildcards/subs\_navigation\_css.txt                        | leftnav.footer                |
| /wildcards/teachers\_footer\_css.txt                        | content.footer                |
| /wildcards/teachers\_footer\_fr\_css.txt                    | content.footer                |
| /wildcards/teachers\_footer\_nf\_css.txt                    | content.footer                |
| /wildcards/teachers\_header\_css.txt                        | conten[pdfRest Free Demo]     |
| /wildcards/teachers\_header\_fr\_css.txt                    | content.header                |
| /wildcards/teachers\_nav\_css.txt                           | leftnav.footer                |
| /wildcards/title\_student\_end\_css.txt                     | student.alert                 |

**Appendix B:[pdfRest Free Demo]s Example Code**

The following example shows the creation of [pdfRest Free Demo]o track college applications. It is comprised of three pages.

- The first page uses tlist\_sql to display a table of all c[pdfRest Free Demo]he U\_APPLICATIONS child table. There is a button link to cr[pdfRest Free Demo]nd each row in the table has a button link to edit that row.[pdfRest Free Demo]ds and editing/deleting existing records takes place on separate web pages.
- The second page is used to edit or delete existing records.
- The third page is used to create new records

**Main Page: /admin/students/applications.html**

<!DOCTYPE html>  
<html>  
<!-- start right frame -->  
<head>  
<title>College Applications</title>  
~\[wc:commonscripts]

<link href="/images/css/screen.css" rel[pdfRest Free Demo]="screen"> <link href="/images/css/print.css" rel="sty[pdfRest Free Demo]nt"> </head>  
<body>  
~\[wc:admin\_header\_frame\_css]<!-- breadcrumb start --><a href="/admin/home.html" target="\_top">Start Page</a> > <a href="home.html?selectstudent=nosearch" target="\_top">Student Selection</a> > College Applications<!-- breadcrumb end -- >~\[wc:admin\_navigation\_frame\_css]

~\[wc:title\_student\_begin\_css]College Applications~\[wc:title\_student\_end\_css]

<!-- start of content and bounding box -->  
<div class="box-round">  
<p style="text-align: center">  
<a href="applications-new.html?frn=~(studentfrn)" id="btnNew" name="btnNew" class="button">New</a>  
</p>

<table class="grid" id="applications">  
<thead>  
<tr style="border:1px solid grey;">  
<th[pdfRest Free Demo]="width:20px;text-align: center">Edit</th> <th c[pdfRest Free Demo]width:200px">Institution</th> <th class="bold" style="width:65px">Request Date</th>

<th class="bold" style="width:75px">Status</th> <th class="bold" style="width:50px">Scholarship?</th> <th class=[pdfRest Free Demo]:65px">Completion Date</th> <th class="bold" style="width:75px">Outcome</th> <th class="bold" style="width:200px">Notes</th>

</tr>  
</thead>  
<tbody>  
~[tlist\_sql;  
select  
studentsdcid,  
ID,  
Institution,  
Request\_Date,

CASE Request\_Status

WHEN 'N' THEN 'New'

WHEN 'U' THEN 'Under Development'

WHEN 'C' THEN 'Complete (Not Submitted)'

WHEN 'S' THEN 'Submitted'

ELSE ''

END Request\_Status,

CASE to\_char(Scholarship)

WHEN '1' THEN 'Yes - ' || TO\_CHAR(Scholarship\_Amount, '$999,999.00')

ELSE 'No'

END Scholarship,  
Completion\_Date,  
CASE Outcome

WHEN 'C' THEN 'Considering'

WHEN 'W' THEN 'Waitlist'

WHEN 'A' THEN 'Accepted'

WHEN 'D' THEN 'Denied'

WHEN 'O' THEN 'Other'

END Outcome,

Notes

FROM U\_APPLICATIONS a  
WHERE StudentsDCID = ~(rn)  
ORDER BY ID]

<tr>  
<td align="center">  
<a href="applications-edit.html?frn=001~(dcid)&id=~(id)"> <img src="/images/btn\_edit.gif" width="16" height="16" border="0"> </a>  
</td>  
<td>~(Institution)</td>  
<td>~(Request\_Date;d)</td>  
<td>~(Request\_Status)</td>  
<td>~(Scholarship)</td>  
<td>~(Completion\_Date;d)</td>  
<td>~(Outcome)</td>  
<td>~(Notes)</td>

</tr>

\[/tlist\_sql]

</tbody>  
</table>  
</div>  
<br>  
<!-- end of content of bounding box -->  
~\[wc:admin\_footer\_frame\_css]

</body>

</html><!-- end right frame -->

**Edit a Record: /admin/students/applications-edit.html**

<!DOCTYPE html>  
<html>  
<!-- start right frame -->  
<head>  
<title>Edit College Application</title>  
~\[wc:UI\_js\_includes]  
~\[wc:commonscripts]

<link href="/images/css/screen.css" rel="stylesheet" media="screen"> <link href="/images/css/print.css" rel="[pdfRest Free Demo]print"> </head>  
<body>  
<form id="edit\_a[pdfRest Free Demo]"applications.html?frn=~(frn)" method="POST"> ~\[wc:admin\_header\_frame\_css]<!-- breadcrumb start --><a hre[pdfRest Free Demo]" target="\_top">Start Page</a> > <a href="home.html?selectstudent=nosearch" target="\_top">Student Selection</a> > Edit College Application<!-- breadcrumb end -->~\[wc:admin\_navigation\_frame\_css]

~\[wc:title\_student\_begin\_css]Edit College Application~\[wc:title\_student\_end\_css]

<!-- start of content and bounding box -->  
<div class="box-round">  
~\[DirectTable.Select:U\_APPLICATIONS;ID:~(gpv.id)] <fieldset id="application">  
<legend>Application Details</legend>  
<div>  
<label style="width: 140px">Institution:</label> <input type="text" name="\[U\_APPLICATIONS]Institution"[pdfRest Free Demo]tution" size="50" />  
</div>  
<div>  
<label style="width: 140px">Request Date:</label>

<inpu[pdfRest Free Demo]"\[U\_APPLICATIONS]Request\_Date" value="" id="Request\_Date" />  
</div>  
<div>  
<label style="width: 140px">Request Status:</label> <select name="\[U\_APPLICATIONS]Request\_Status" id="Request\_Status"> <option value="">Select a Status</option>  
<option value="N">New</option>  
<option value="U">Under Development</option> <option value="C">Complete (Not Submitted)</option> <option value="S">Submitted</option>  
</select>  
</div>  
<div>  
<label style="width: 140px">Scholarship:</label> <input type="checkbox" name="\[U\_APPLICATIONS]Scholars[pdfRest Free Demo]Scholarship" />&nbsp;(Yes)

</div>  
<div>  
<label style="width: 140px">Scholarship Amount:</label> $<input type="text" name="\[U\_APPLICATIONS]Scholarship\_Amount" value="" id="Scholarship\_Amount" size="8" />  
</div>  
<div>  
<label style="width: 140px">Completion Date:</label> <input type="text" name="\[U\_APPLICATIONS]Completion\_Date" value="" id="Completion\_Date" />  
</div>  
<div>  
<label style="width: 140px">Outcome:</label> <select name="\[U\_APPLICATIONS]Outcome" id="Outcome"> <option value="">Select an Outcome</option>  
<option value="C">Considering</option>  
<option value="W">Waitlist</option>  
<option value="A">Accepted</option>  
<option value="D">Denied</option>  
<option value="O">Other</option>  
</select>  
</div>  
<div>  
<label style="width: 140px">Notes:</label>  
<textarea name="\[U\_APPLICATIONS]Notes" cols="50" r[pdfRest Free Demo]/> </div>  
</fieldset>

<div class="button-row" id="row1">  
<input type="hidden" name="ac" value="prim"> <button name="btnSubmit" id="btnSubmit" type="submit">Submit</button> </div>  
</form>

<div class="button-row" id="row2">  
<form id="delete\_bu[pdfRest Free Demo]cations.html?frn=~(studentfrn)" method="post">  
<input[pdfRest Free Demo]="ac" value="prim" /> <input type="hidden" name="DD-U\_APPLICATIONS.ID:~(gpv.id)" value="1" /> <button class=[pdfRest Free Demo]d="btnDelete">Delete</button> </form>  
</div>  
</div>  
~\[wc:admin\_footer\_frame\_css]  
</body>  
</html><!-- end right frame -->

**Add a New Record: /admin/students/applications-new.html**

<!DOCTYPE html>  
<html>  
<!-- start right frame -->  
<head>  
<title>New College Application</title>  
~\[wc:UI\_js\_includes]  
~\[wc:commonscripts]

<link hr[pdfRest Free Demo]een.css" rel="stylesheet" media="screen"> <link href="[pdfRest Free Demo]ss" rel="stylesheet" media="print"> </head>  
<body>  
<form id="new\_application" action="applications.html?frn=~(frn)" method="POST"> ~\[wc:admin\_header\_frame\_css]<!-- breadcrumb start --><a href="/admin/home.html" target="\_top">Start Page</a> > <a href="home.html?selectstudent=nosearch" target="\_top">Student Selection</a> > New College Application<!-- breadcrumb end - ->~\[wc:admin\_navigation\_frame\_css]

~\[wc:title\_student\_begin\_css]New College Application~\[wc:title\_student\_end\_css]

<!-- start of content and bounding box -->  
<div class="box-round">  
~\[DirectTable.Select:U\_APPLICATIONS;ID:-1] <!-- Using hard-coded -1 because this is a new record -->  
<fieldset id="application">  
<legend>Application Details</legend>  
<div>

<label style="width: 140px">Institution:</label> <input type="text" name="\[U\_APPLICATIONS]Institution" val[pdfRest Free Demo]on" size="50" />  
</div>  
<div>  
<label style="width: 140px">Request Date:</label> <input type="text" name="\[U\_APPLICATIONS]Request\_Date" value="" id="Request\_Date" />  
</div>  
<div>  
<label style="width: 140px">Request Status:</label> <select name="\[U\_APPLICATIONS]Request\_Status" id="Request\_Status"> <option value="">Select a Status</option>  
<option value="N">New</option>  
<option value="U">Under Development</option> <option value="C">Complete (Not Submitted)</option> <option value="S">Submitted</option>  
</select>  
</div>  
<div>  
<label style="width: 140px">Scholarship:</label> <input type="checkbox" name="\[U\_APPLICATIONS]Scholarship" value="1" id="Scholarship" />&nbsp;(Yes)

</div>  
<div>  
<label style="width: 140px">Scholarship Amount:</label> $<input type="text" name="\[U\_APPLICATIONS]Scholarship\_Amount" value="" id="Scholarship\_Amount" size="8" />  
</div>  
<div>  
<label style="width: 140px">Completion Date:</label> <input type="text" name="\[U\_APPLICATIONS]Completion\_D[pdfRest Free Demo]ompletion\_Date" />  
</div>  
<div>  
<label style="width: 140px">Outcome:</label> <select name="\[U\_APPLICATIONS]Outcome" id="Outcome"> <option value="">Select an Outcome</option>  
<option value="C">Considering</option>  
<option value="W">Waitlist</option>  
<option value="A">Accepted</option>  
<option value="D">Denied</option>  
<option value="O">Other</option>  
</select>  
</div>  
<div>  
<label style="width: 140px">Notes:</label>  
<textarea name="\[U\_APPLICATIONS]Not[pdfRest Free Demo]"5" id="Notes" /> </div>

<div class="button-row">  
<input type="hidden" name="ac" value="prim"> <b[pdfRest Free Demo]it" id="btnSubmit" type="submit">Submit</button> </div>

</fieldset>  
</div>  
~\[wc:admin\_footer\_frame\_css]  
</form>

</body>  
</html><!-- end right frame -->

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
