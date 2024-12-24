@echo off
set BUNDLE=\\mars\_Src\InstaFind\bundle\InstaFind.bundle
git bundle create %BUNDLE% --all
git bundle verify %BUNDLE%