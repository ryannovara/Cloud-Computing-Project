# GitHub Deployment Guide

## Step 1: Verify .gitignore is Set Up ✅
Your `.gitignore` file is already configured to exclude:
- `local.settings.json` (secrets)
- Build folders (`bin/`, `obj/`)
- Azurite storage files
- Temporary files

## Step 2: Initialize Git Repository (if not already done)

Open terminal in VS Code or your project folder and run:

```bash
cd "/Applications/Cloud Computing Midterm"
git init
```

## Step 3: Create Empty Repository on GitHub

1. Go to [GitHub.com](https://github.com) and log in
2. Click the **+** icon in the top-right corner
3. Select **New repository**
4. Name it: `cloud-computing-midterm` (or your preferred name)
5. **DO NOT** check "Initialize with README" (keep it empty)
6. Click **Create repository**

## Step 4: Add and Commit Your Files

```bash
# Add all files (respecting .gitignore)
git add .

# Check what will be committed (verify local.settings.json is NOT listed)
git status

# Commit your files
git commit -m "Initial commit: Azure Functions project with Key Vault, SQL, Logic App, and Application Insights"
```

## Step 5: Connect to GitHub and Push

After creating the repository on GitHub, you'll see instructions. Use these commands:

```bash
# Add GitHub as remote (replace YOUR_USERNAME with your GitHub username)
git remote add origin https://github.com/YOUR_USERNAME/cloud-computing-midterm.git

# Rename branch to main (if needed)
git branch -M main

# Push to GitHub
git push -u origin main
```

**Note:** You may be prompted for GitHub credentials. Use a Personal Access Token (not your password):
- Go to GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
- Generate new token with `repo` permissions
- Use the token as your password when prompted

## Step 6: Verify on GitHub

1. Go to your repository on GitHub
2. Verify that:
   - ✅ All source code files are present
   - ✅ `local.settings.json` is **NOT** visible (it's ignored)
   - ✅ `bin/` and `obj/` folders are **NOT** visible
   - ✅ Azurite files are **NOT** visible

## Files That Should Be in GitHub:

✅ **Should be committed:**
- `HttpTrigger1.cs`
- `Program.cs`
- `Cloud Computing Midterm.csproj`
- `host.json`
- `Models/Game.cs`
- `create_table.sql`
- `update_table_schema.sql`
- `.gitignore`
- `Properties/launchSettings.json`

❌ **Should NOT be committed (already in .gitignore):**
- `local.settings.json` (contains secrets!)
- `bin/` folder
- `obj/` folder
- `__azurite_*.json` files
- `__blobstorage__/` folder
- `*.zip` files
- `publish/` folder

## Troubleshooting

**If you get "remote origin already exists":**
```bash
git remote remove origin
git remote add origin https://github.com/YOUR_USERNAME/cloud-computing-midterm.git
```

**If you need to update files later:**
```bash
git add .
git commit -m "Update: description of changes"
git push
```

