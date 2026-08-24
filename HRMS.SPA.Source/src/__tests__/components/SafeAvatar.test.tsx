import { describe, it, expect } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { SafeAvatar } from '@/components/shared/SafeAvatar';

describe('SafeAvatar', () => {
  it('renders initials when no avatarUrl is provided', () => {
    render(<SafeAvatar profile={{ firstName: 'Ratan', lastName: 'Sharma' }} />);
    expect(screen.getByText('RS')).toBeInTheDocument();
  });

  it('renders initials from fullName when firstName/lastName are absent', () => {
    render(<SafeAvatar profile={{ fullName: 'Priya Nair' }} />);
    expect(screen.getByText('PN')).toBeInTheDocument();
  });

  it('renders "?" when no profile is provided', () => {
    render(<SafeAvatar profile={null} />);
    expect(screen.getByText('?')).toBeInTheDocument();
  });

  it('renders "?" when profile is undefined', () => {
    render(<SafeAvatar />);
    expect(screen.getByText('?')).toBeInTheDocument();
  });

  it('renders an img when avatarUrl is provided', () => {
    render(
      <SafeAvatar
        profile={{
          firstName: 'Ratan',
          lastName: 'Sharma',
          avatarUrl: 'https://example.com/avatar.jpg',
        }}
      />,
    );
    const img = screen.getByRole('img');
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute('src', 'https://example.com/avatar.jpg');
  });

  it('falls back to initials when the image fails to load', () => {
    render(
      <SafeAvatar
        profile={{
          firstName: 'Ratan',
          lastName: 'Sharma',
          avatarUrl: 'https://broken-url.example.com/avatar.jpg',
        }}
      />,
    );
    const img = screen.getByRole('img');
    // Simulate a broken image by firing the error event
    fireEvent.error(img);
    // The initials fallback should now be rendered
    expect(screen.getByText('RS')).toBeInTheDocument();
  });

  it('applies a custom size class', () => {
    const { container } = render(
      <SafeAvatar profile={{ firstName: 'A', lastName: 'B' }} size="h-12 w-12" />,
    );
    expect(container.firstChild).toHaveClass('h-12', 'w-12');
  });
});
