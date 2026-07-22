// Deterministic Testing-only users seeded by fixtures/seed-e2e.sql. Never Production accounts.
// Credentials are overridable via env for CI, defaulting to the isolated E2E seed values.

export type Role = 'Customer' | 'CustomerVIP' | 'Admin' | 'SuperAdmin';

export interface TestUser {
  readonly role: Role;
  readonly mobile: string;
  readonly password: string;
  readonly label: string;
  /** True for the admin panel schemes (Admin / SuperAdmin). */
  readonly isAdmin: boolean;
}

const PASSWORD =
  process.env.E2E_QA_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'E2E-Admin-Only-aA1!';

export const USERS: Record<Role, TestUser> = {
  Customer: {
    role: 'Customer',
    mobile: process.env.E2E_CUSTOMER_MOBILE ?? '09120000013',
    password: PASSWORD,
    label: 'E2E Customer',
    isAdmin: false
  },
  CustomerVIP: {
    role: 'CustomerVIP',
    mobile: process.env.E2E_VIP_MOBILE ?? '09120000014',
    password: PASSWORD,
    label: 'E2E VIP Customer',
    isAdmin: false
  },
  Admin: {
    role: 'Admin',
    mobile: process.env.E2E_PLAIN_ADMIN_MOBILE ?? '09120000012',
    password: PASSWORD,
    label: 'E2E Admin',
    isAdmin: true
  },
  SuperAdmin: {
    role: 'SuperAdmin',
    mobile: process.env.E2E_ADMIN_MOBILE ?? '09120000011',
    password: PASSWORD,
    label: 'E2E Monitoring Admin',
    isAdmin: true
  }
};

export const user = (role: Role): TestUser => USERS[role];
