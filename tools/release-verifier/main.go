// Verifies release approvals offline using the official Sigstore verifier.
package main

import (
	"bytes"
	_ "embed"
	"errors"
	"fmt"
	"io"
	"os"

	"github.com/sigstore/sigstore-go/pkg/bundle"
	"github.com/sigstore/sigstore-go/pkg/root"
	"github.com/sigstore/sigstore-go/pkg/verify"
)

//go:embed trusted-root.json
var trustedRoot []byte

const issuer = "https://token.actions.githubusercontent.com"
const workflow = `^https://github\.com/ClankerWorldOrg/ClankerWorld/\.github/workflows/(nightly|release)\.yml@refs/heads/main$`

func verifyApproval(payload, proof []byte) error {
	trusted, err := root.NewTrustedRootFromJSON(trustedRoot)
	if err != nil {
		return err
	}
	entity := &bundle.Bundle{}
	if err = entity.UnmarshalJSON(proof); err != nil {
		return err
	}
	if !entity.MinVersion("0.3") {
		return errors.New("a complete Sigstore verification bundle is required")
	}
	return verifyEntity(payload, entity, trusted)
}

func verifyEntity(payload []byte, entity verify.SignedEntity, trusted root.TrustedMaterial) error {
	verifier, err := newVerifier(trusted)
	if err != nil {
		return err
	}
	identity, err := releaseIdentity()
	if err != nil {
		return err
	}
	_, err = verifier.Verify(entity, verify.NewPolicy(verify.WithArtifact(bytes.NewReader(payload)),
		verify.WithCertificateIdentity(identity)))
	return err
}

func newVerifier(trusted root.TrustedMaterial) (*verify.Verifier, error) {
	return verify.NewVerifier(trusted, verify.WithTransparencyLog(1),
		verify.WithSignedCertificateTimestamps(1), verify.WithObserverTimestamps(1))
}

func releaseIdentity() (verify.CertificateIdentity, error) {
	return verify.NewShortCertificateIdentity(issuer, "", "", workflow)
}

func run(args []string) error {
	if len(args) != 2 {
		return errors.New("pass the approval payload and its Sigstore bundle")
	}
	limits := []int64{4096, 262144}
	contents := make([][]byte, 2)
	for index, path := range args {
		info, err := os.Stat(path)
		if err != nil || !info.Mode().IsRegular() || info.Size() > limits[index] {
			return errors.New("invalid release approval input")
		}
		file, err := os.Open(path)
		if err != nil {
			return errors.New("could not open release approval input")
		}
		contents[index], err = io.ReadAll(io.LimitReader(file, limits[index]+1))
		file.Close()
		if err != nil || int64(len(contents[index])) > limits[index] {
			return errors.New("could not read release approval input")
		}
	}
	return verifyApproval(contents[0], contents[1])
}

func main() {
	if err := run(os.Args[1:]); err != nil {
		fmt.Fprintln(os.Stderr, "Release approval verification failed.")
		os.Exit(1)
	}
}
