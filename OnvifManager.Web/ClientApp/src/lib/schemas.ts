import { z } from 'zod';

const IPV4_REGEX = /^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}$/;
// Pragmatic IPv6 matcher; full RFC parser is overkill for an admin form.
const IPV6_REGEX = /^[0-9a-fA-F:]+$/;

function isIpAddress(value: string): boolean {
  if (IPV4_REGEX.test(value)) return true;
  if (value.includes(':') && IPV6_REGEX.test(value) && value.length >= 2) return true;
  return false;
}

const baseCameraSchema = z.object({
  name: z.string().trim().min(1, 'Name is required').max(100, 'Max 100 characters'),
  ip: z
    .string()
    .trim()
    .min(1, 'IP is required')
    .refine(isIpAddress, 'Enter a valid IPv4 or IPv6 address'),
  port: z.coerce
    .number({ invalid_type_error: 'Port must be a number' })
    .int('Port must be integer')
    .min(1, 'Port must be >= 1')
    .max(65535, 'Port must be <= 65535')
    .default(80),
  username: z.string().trim().min(1, 'Username is required').max(100, 'Max 100 characters'),
});

export const createCameraSchema = baseCameraSchema.extend({
  password: z.string().min(1, 'Password is required').max(200, 'Max 200 characters'),
});

export const updateCameraSchema = baseCameraSchema.extend({
  // Empty string means "keep current password".
  password: z
    .string()
    .max(200, 'Max 200 characters')
    .optional()
    .or(z.literal('')),
});

export type CreateCameraForm = z.infer<typeof createCameraSchema>;
export type UpdateCameraForm = z.infer<typeof updateCameraSchema>;
