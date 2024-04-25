Contribution overview
=====================

General guidance for contributing to the bITdevKit repositories
---------------------------------------------------------------

Thank you for your interest in the bITdevKit! This document provides the guidelines for how to contribute to the bITdevKit project through issues and pull-requests.

Issue types
-----------
Before you submit an issue, please search the issue tracker. An issue for your problem might already exist and the discussion might inform you of workarounds readily available.

- Issue/Bug: You’ve found a bug with the code, and want to report it, or create an issue to track the bug. 
- Issue/Discussion: You have something to discuss.
- Issue/Proposal: Used for items that propose a new idea or functionality.
- Issue/Question: Use this issue type, if you need help or have a question.

Before submitting
-----------------

Before submitting an issue or pull request, please do the following:
- A minimal reproduction is required. It allows us to quickly confirm a bug (or point out a coding problem) as well as confirm that we are fixing the right problem. 
- Search the issue tracker to ensure that the issue is not a duplicate.


Pull Requests
-------------

The BridgigIT team reserves the right not to review pull requests from community members or decline pull requests. 


1. Make sure there is an issue (bug or proposal) open for the problem you are trying to fix. If there isn't, please open one.			
1. Fork the repository and create a new branch
1. Create your change and write tests
1. Update relevant documentation for the change
1. Commit with Developer Certificate of Origin (Signing your work) and open a PR



Developer Certificate of Origin: Signing your work
--------------------------------------------------

The sign-off is a simple line at the end of the explanation for the patch, which certifies that you wrote it or otherwise have the right to pass it on as an open-source patch. The rules are pretty simple: if you can certify the below (from https://developercertificate.org/):

You have to sign-off that you adhere to these requirements by adding a Signed-off-by line to commit messages.

```
Original commit message
Signed-off-by: max mustermann<max@bridging-it.de>
```

If you didn't sign your commit, you can replay your changes, sign them and fore push them:

```
git checkout <branch-name>
git commit --amend --no-edit --signoff
```

Code of Conduct 
---------------
Please see the bITdevKit community code of conduct: CODE-OF-CONDUCT.md .