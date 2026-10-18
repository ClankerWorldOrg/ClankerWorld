package main

import (
	"encoding/hex"
	"os"
	"testing"

	"github.com/sigstore/sigstore-go/pkg/bundle"
	"github.com/sigstore/sigstore-go/pkg/fulcio/certificate"
	"github.com/sigstore/sigstore-go/pkg/root"
	"github.com/sigstore/sigstore-go/pkg/testing/ca"
	"github.com/sigstore/sigstore-go/pkg/tlog"
	"github.com/sigstore/sigstore-go/pkg/verify"
)

func TestReleaseIdentity(t *testing.T) {
	identity, err := releaseIdentity()
	if err != nil {
		t.Fatal(err)
	}
	for _, test := range []struct {
		name, san, oidc string
		accepted        bool
	}{
		{"nightly", "https://github.com/ClankerWorldOrg/ClankerWorld/.github/workflows/nightly.yml@refs/heads/main", issuer, true},
		{"release", "https://github.com/ClankerWorldOrg/ClankerWorld/.github/workflows/release.yml@refs/heads/main", issuer, true},
		{"fork", "https://github.com/other/ClankerWorld/.github/workflows/nightly.yml@refs/heads/main", issuer, false},
		{"pull request", "https://github.com/ClankerWorldOrg/ClankerWorld/.github/workflows/nightly.yml@refs/pull/1/merge", issuer, false},
		{"other workflow", "https://github.com/ClankerWorldOrg/ClankerWorld/.github/workflows/ci.yml@refs/heads/main", issuer, false},
		{"suffix", "https://github.com/ClankerWorldOrg/ClankerWorld/.github/workflows/nightly.yml@refs/heads/main/extra", issuer, false},
		{"other issuer", "https://github.com/ClankerWorldOrg/ClankerWorld/.github/workflows/nightly.yml@refs/heads/main", "https://github.com/login/oauth", false},
	} {
		t.Run(test.name, func(t *testing.T) {
			err := identity.Verify(certificate.Summary{SubjectAlternativeName: test.san, Issuer: test.oidc})
			if (err == nil) != test.accepted {
				t.Fatalf("accepted=%v, error=%v", err == nil, err)
			}
		})
	}
}

// This public fixture is Cosign's actual published Windows binary approval,
// not a proof invented by the code under test. The digest is pinned separately
// from its GitHub release asset. Verification uses the embedded production root.
func TestRealBundleAndTampering(t *testing.T) {
	proof, err := os.ReadFile("testdata/cosign-v3.1.3-windows.sigstore.json")
	if err != nil {
		t.Fatal(err)
	}
	entity := &bundle.Bundle{}
	if err = entity.UnmarshalJSON(proof); err != nil {
		t.Fatal(err)
	}
	trusted, err := root.NewTrustedRootFromJSON(trustedRoot)
	if err != nil {
		t.Fatal(err)
	}
	v, err := newVerifier(trusted)
	if err != nil {
		t.Fatal(err)
	}
	id, err := verify.NewShortCertificateIdentity("https://accounts.google.com", "", "keyless@projectsigstore.iam.gserviceaccount.com", "")
	if err != nil {
		t.Fatal(err)
	}
	digest, err := hex.DecodeString("9fe59be0eca1271873ce019061335eb1ac419b7059202e797828467ddabe33be")
	if err != nil {
		t.Fatal(err)
	}
	policy := func(hash []byte, identity verify.CertificateIdentity) verify.PolicyBuilder {
		return verify.NewPolicy(verify.WithArtifactDigest("sha256", hash), verify.WithCertificateIdentity(identity))
	}
	if _, err = v.Verify(entity, policy(digest, id)); err != nil {
		t.Fatal(err)
	}
	changed := append([]byte{}, digest...)
	changed[0] ^= 1
	if _, err = v.Verify(entity, policy(changed, id)); err == nil {
		t.Fatal("tampered digest was accepted")
	}
	if _, err = v.Verify(withoutLog{entity}, policy(digest, id)); err == nil {
		t.Fatal("missing transparency proof was accepted")
	}
	productionID, _ := releaseIdentity()
	if _, err = v.Verify(entity, policy(digest, productionID)); err == nil {
		t.Fatal("another project's valid proof was accepted as ClankerWorld")
	}
}

type withoutLog struct{ verify.SignedEntity }

func (withoutLog) TlogEntries() ([]*tlog.Entry, error) { return nil, nil }
func (withoutLog) HasInclusionProof() bool             { return false }
func (withoutLog) HasInclusionPromise() bool           { return false }

func TestCertificateTransparencyCannotBeOmitted(t *testing.T) {
	service, err := ca.NewVirtualSigstore()
	if err != nil {
		t.Fatal(err)
	}
	payload := []byte("approval payload")
	entity, err := service.Sign("https://github.com/ClankerWorldOrg/ClankerWorld/.github/workflows/nightly.yml@refs/heads/main", issuer, payload)
	if err != nil {
		t.Fatal(err)
	}
	// The upstream virtual service supplies real artifact signatures, log proofs
	// and timestamps, but no SCT. That absence must refuse the approval.
	if verifyEntity(payload, entity, service) == nil {
		t.Fatal("certificate without an SCT was accepted")
	}
}

func TestMalformedBundlesAreRefused(t *testing.T) {
	for _, proof := range []string{`null`, `{}`, `not json`, `{"mediaType":"application/vnd.dev.sigstore.bundle.v0.3+json"}`} {
		if verifyApproval([]byte("payload"), []byte(proof)) == nil {
			t.Fatal("invalid bundle was accepted")
		}
	}
}
