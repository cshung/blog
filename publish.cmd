cd src\index
dotnet run
cd ..\..
move /Y src\index\index.js static\index.js
echo Index rebuilt.
attrib -h ..\cshung.github.io\.git
move ..\cshung.github.io\.git ..
hugo
robocopy /mir /z public ..\cshung.github.io 
move ..\.git ..\cshung.github.io
attrib +h ..\cshung.github.io\.git
cd ..\cshung.github.io
git add .
git status
cd ..\blog
rd /s/q public
