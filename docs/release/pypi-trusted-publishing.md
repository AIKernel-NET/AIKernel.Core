# PyPI Trusted Publishing

AIKernel Python packages are published from each package repository by GitHub
Actions and PyPI Trusted Publishing. Do not store long-lived PyPI API tokens in
GitHub Secrets for PyPI release publishing.

## Workflow

Each package repository uses `.github/workflows/publish-pypi.yml`.

The publish job must:

- use the GitHub Environment named `pypi`
- set `permissions: id-token: write`
- use `pypa/gh-action-pypi-publish@release/v1`
- omit `username`, `password`, and `api-token`

The workflow separates build and publish:

1. `build` creates wheel and sdist, runs `twine check`, and uploads a package
   artifact.
2. `publish` downloads the artifact and uploads it to PyPI with OIDC.

## PyPI Projects

Configure a Trusted Publisher for each PyPI project that will be released:

| PyPI project | GitHub repository | Workflow filename | Environment |
| --- | --- | --- | --- |
| `aikernel-net` | `AIKernel-NET/AIKernel.Core` | `publish-pypi.yml` | `pypi` |
| `aikernel-tools` | `AIKernel-NET/AIKernel.Tools` | `publish-pypi.yml` | `pypi` |
| `aikernel-providers` | `AIKernel-NET/AIKernel.Providers` | `publish-pypi.yml` | `pypi` |
| `aikernel-governance` | `AIKernel-NET/AIKernel.Control` | `publish-pypi.yml` | `pypi` |
| `aikernel-wasm` | `AIKernel-NET/AIKernel.Wasm` | `publish-pypi.yml` | `pypi` |
| `aikernel-cuda13-libtorch2-12-win-x64` | `AIKernel-NET/AIKernel.Cuda13.0` | `publish-python.yml` | `pypi` |

Verify the repository owner and repository name against GitHub before saving
the PyPI publisher entry. PyPI requires exact matches.

## GitHub Environment

Create the GitHub Environment before publishing in every package repository:

1. Open repository Settings -> Environments.
2. Create an environment named `pypi`.
3. Add required reviewers when possible.
4. Make sure the environment name exactly matches the PyPI Trusted Publisher
   environment name.

If the GitHub Environment name and PyPI Trusted Publisher environment name do
not match, publishing will fail.

## Remove Old Secrets

Remove old long-lived PyPI token secrets from GitHub repository or organization
settings after the Trusted Publisher entries are ready. The publishing workflow
must not depend on:

- `PYPI_TOKEN`
- `PYPI_API_TOKEN`
- `TEST_PYPI_TOKEN`
- `TWINE_USERNAME`
- `TWINE_PASSWORD`

Do not reintroduce `twine upload` with token credentials for PyPI release
publishing.

## First Migration Run

For the first migration, validate with a smaller package before publishing the
core package:

1. Configure the `pypi` environment in `AIKernel.Tools`.
2. Configure the Trusted Publisher for `aikernel-tools` on PyPI.
3. Push a tag such as `py-aikernel-tools-0.1.3`.
4. Confirm the workflow builds and publishes only `aikernel-tools`.
5. On the PyPI file detail page, confirm `Uploaded using Trusted Publishing?`
   shows `Yes`.
6. Smoke test installation in a clean environment:

   ```powershell
   py -m venv .venv-pypi-smoke
   .\.venv-pypi-smoke\Scripts\python -m pip install --upgrade pip
   .\.venv-pypi-smoke\Scripts\python -m pip install aikernel-tools==0.1.3
   .\.venv-pypi-smoke\Scripts\python -c "import aikernel_tools"
   ```

After the smaller package succeeds, configure the remaining PyPI projects.

## Release Checklist

Before pushing a release tag:

- Confirm the Python package version has been updated.
- Build wheel and sdist locally for the selected package.
- Confirm the publish workflow runs from the intended `v*` or
  `py-<project-name>-*` tag.
- Confirm the publish job has `id-token: write`.
- Confirm the publish job uses the `pypi` GitHub Environment.
- Confirm the workflow contains no PyPI API token, username, or password
  configuration.
- Confirm the PyPI Trusted Publisher entry exists for the project.

After publishing:

- Confirm the PyPI file detail page says `Uploaded using Trusted Publishing?`
  `Yes`.
- Smoke test with `pip install <project>==<version>` from a clean virtual
  environment.
- Keep NuGet and .NET release workflows separate from PyPI publishing.
