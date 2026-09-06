import React from 'react';
import { render } from '@testing-library/react';
import App from './App';

test('renders the application shell', () => {
  const { getByText } = render(<App />);
  expect(getByText('Git: Pjx Project')).toBeInTheDocument();
});
