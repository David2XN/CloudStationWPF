rmdir /s /q data0
rmdir /s /q data1
rmdir /s /q data2
rmdir /s /q data3
rmdir /s /q data4
rmdir /s /q data5

del "127.0.0.1.4220.txt"
del "127.0.0.1.4221.txt"
del "127.0.0.1.4222.txt"
del "127.0.0.1.4223.txt"
del "127.0.0.1.4224.txt"
del "127.0.0.1.4225.txt"

mkdir data0
copy "a.txt" "./data0/a.txt"

