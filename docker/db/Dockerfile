FROM pomelofoundation/mysql-windows:8-ltsc2019

ENV MYSQL_DATABASE obm

COPY ./run.ps1 C:/tools/mysql/current/run.ps1
COPY ./init C:/init

EXPOSE 3306
RUN Set-Service mysql -StartupType Manual
CMD C:\tools\mysql\current\run.ps1
