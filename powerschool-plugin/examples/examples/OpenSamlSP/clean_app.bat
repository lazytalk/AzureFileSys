@echo off

if exist ".\apache-tomcat-8\webapps\spring-security-saml2-sp.war" goto doit
echo Nothing to do...
:doit

echo cleaning webapps folder...
DEL .\apache-tomcat-8\webapps\spring-security-saml2-sp.war
RD .\apache-tomcat-8\webapps\spring-security-saml2-sp /S /Q
echo cleaning all target folders....
call mvn clean

:end
