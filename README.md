# Examensprojekt, Secure Software development

## Dette projekt vil demonstrere følgende

- [ ] Krypteret chat mellem 2 parter
- [ ] Single sign on for chat-server og historik/log-server
- [ ] Best practices for CI/CD
    - [ ] SBOM
    - [ ] Branch protection
    - [ ] automated build


### Signed commits
Commits are signed with an SSH key. It could have been PGP, but SSH was chosen
```bash
ssh-keygen -t ed25519 -C "bjoern@goettler.dk" -f ~/.ssh/ssd_examen
cat ~/.ssh/ssd_examen.pub
```

Then the public key was uploaded to github, as a signing key
The local git is configured to use the key
```bash
git config gpg.format ssh
git config user.signingkey ~/.ssh/ssd_examen
```

The recommendation is to add the key to the ssh-agent. Otherwise there can be issues with git accesing the key, and if it is password protected, the password only has to be entered once per session
```bash
eval "$(ssh-agent -s)"
ssh-add ~/.ssh/ssd_examen
```
Now github shows that the committer is verified
![Signed commit](images/github_showing_signed_commit.png)

## CI/CD

- Github
 - CODEOWNERS file
 - Branch protection rules
