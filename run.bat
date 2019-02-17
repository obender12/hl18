FOR /f "tokens=*" %%i IN ('docker ps -q') DO docker stop %%i
docker run -it -p 80:80 -v "c:\tmp":/data --memory 2048m --memory-swap 2048m --ulimit nofile=32768:32768 -t dumb /data