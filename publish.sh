mv ../cshung.github.io/.git ..
rm -rf ../cshung.github.io
export HUGO_ENV=production
hugo
mv ./public ../cshung.github.io
mv ../.git ../cshung.github.io
cd ../cshung.github.io
git add .
git status
cd ../blog
