<%@ page import="org.springframework.security.saml.SAMLCredential" %>
<%@ page import="org.springframework.security.core.context.SecurityContextHolder" %>
<%@ taglib prefix="c" uri="http://java.sun.com/jsp/jstl/core" %>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN" "http://www.w3.org/TR/html4/loose.dtd">
<html>
<head>
    <title>Spring Security SAML Sample Application - User Authenticated</title>
    <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
</head>
<body>

<%
    String rawXML = "bar";
    if (SAMLCredential.currentRawXMLResponse != null) {
    	rawXML = SAMLCredential.currentRawXMLResponse;
    }
    String lastFirst="";
	    if (SAMLCredential.lastFirst != null) {
    	lastFirst = SAMLCredential.lastFirst;
    }
%>
<font face="Arial,sans-serif" size=5><b>Welcome <%= lastFirst %> to Spring Security SAML Sample Application!</b></font>
<p>
<table>
    <tr>
        <td><font face="Arial,sans-serif" size=2><b>AuthnResponse:</b></font></td>
    </tr>
    <tr>
        <td><textarea rows="75" name="rawXML" cols="200" readonly="readonly"><%= rawXML %></textarea></font></td>
    </tr>
</table>
</p>

<p>
    <a href="<c:url value="/saml/logout?local=true"/>"><font face="Arial,sans-serif" size=2>Logout</font></a>
</p>

</body>
</html>